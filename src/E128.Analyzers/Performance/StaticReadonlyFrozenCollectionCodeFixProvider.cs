using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128027: transforms <c>static readonly HashSet&lt;T&gt;</c> / <c>Dictionary&lt;K,V&gt;</c>
///     to <c>FrozenSet&lt;T&gt;</c> / <c>FrozenDictionary&lt;K,V&gt;</c> by wrapping the initializer
///     with <c>.ToFrozenSet()</c> or <c>.ToFrozenDictionary()</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StaticReadonlyFrozenCollectionCodeFixProvider))]
[Shared]
public sealed class StaticReadonlyFrozenCollectionCodeFixProvider : CodeFixProvider
{
    private const string HashSetMetadataName = "System.Collections.Generic.HashSet`1";
    private const string DictionaryMetadataName = "System.Collections.Generic.Dictionary`2";
    private const string FrozenNamespace = "System.Collections.Frozen";

    public override ImmutableArray<string> FixableDiagnosticIds => [StaticReadonlyFrozenCollectionAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var declaration = root.FindNode(context.Diagnostics[0].Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<VariableDeclarationSyntax>()
            .FirstOrDefault();

        if (declaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Convert to frozen collection",
                ct => ConvertToFrozenAsync(context.Document, declaration, ct),
                StaticReadonlyFrozenCollectionAnalyzer.DiagnosticId),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ConvertToFrozenAsync(
        Document document,
        VariableDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null || root is null)
        {
            return document;
        }

        var variable = declaration.Variables.FirstOrDefault();
        if (variable?.Initializer?.Value is null)
        {
            return document;
        }

        if (!TryResolveFieldType(semanticModel, variable, cancellationToken, out var fieldType, out var isHashSet))
        {
            return document;
        }

        var newDeclaration = BuildFrozenDeclaration(declaration, variable, fieldType, isHashSet);
        var newRoot = AddUsingIfMissing(root.ReplaceNode(declaration, newDeclaration));
        return document.WithSyntaxRoot(newRoot);
    }

    private static bool TryResolveFieldType(
        SemanticModel semanticModel,
        VariableDeclaratorSyntax variable,
        CancellationToken cancellationToken,
        out INamedTypeSymbol fieldType,
        out bool isHashSet)
    {
        fieldType = null!;
        isHashSet = false;

        if (semanticModel.GetDeclaredSymbol(variable, cancellationToken) is not IFieldSymbol fieldSymbol)
        {
            return false;
        }

        if (fieldSymbol.Type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
        {
            return false;
        }

        var hashSetType = semanticModel.Compilation.GetTypeByMetadataName(HashSetMetadataName);
        var dictionaryType = semanticModel.Compilation.GetTypeByMetadataName(DictionaryMetadataName);
        var originalDefinition = namedType.OriginalDefinition;

        if (hashSetType is not null && SymbolEqualityComparer.Default.Equals(originalDefinition, hashSetType))
        {
            fieldType = namedType;
            isHashSet = true;
            return true;
        }

        if (dictionaryType is not null && SymbolEqualityComparer.Default.Equals(originalDefinition, dictionaryType))
        {
            fieldType = namedType;
            isHashSet = false;
            return true;
        }

        return false;
    }

    private static VariableDeclarationSyntax BuildFrozenDeclaration(
        VariableDeclarationSyntax declaration,
        VariableDeclaratorSyntax variable,
        INamedTypeSymbol fieldType,
        bool isHashSet)
    {
        var frozenTypeName = isHashSet
            ? $"FrozenSet<{fieldType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}>"
            : $"FrozenDictionary<{fieldType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}, {fieldType.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}>";

        var extensionMethod = isHashSet ? "ToFrozenSet" : "ToFrozenDictionary";

        var newTypeSyntax = SyntaxFactory.ParseTypeName(frozenTypeName)
            .WithTriviaFrom(declaration.Type);

        var explicitInitializer = MakeExplicitInitializer(variable.Initializer!.Value, fieldType);

        var wrappedExpression = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                explicitInitializer,
                SyntaxFactory.IdentifierName(extensionMethod)));

        return declaration
            .WithType(newTypeSyntax)
            .WithVariables(
                SyntaxFactory.SeparatedList(
                    [
                        variable.WithInitializer(
                            variable.Initializer.WithValue(wrappedExpression))
                    ]));
    }

    /// <summary>
    ///     Converts implicit <c>new()</c> to explicit <c>new HashSet&lt;T&gt;()</c> so the
    ///     initializer compiles when the field type changes to FrozenSet/FrozenDictionary.
    /// </summary>
    private static ExpressionSyntax MakeExplicitInitializer(ExpressionSyntax initializer, INamedTypeSymbol fieldType)
    {
        if (initializer is not ImplicitObjectCreationExpressionSyntax implicitNew)
        {
            return initializer;
        }

        var explicitType = SyntaxFactory.ParseTypeName(
            fieldType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

        return SyntaxFactory.ObjectCreationExpression(explicitType)
            .WithArgumentList(implicitNew.ArgumentList)
            .WithInitializer(implicitNew.Initializer);
    }

    private static SyntaxNode AddUsingIfMissing(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        if (compilationUnit.Usings.Any(u =>
                string.Equals(u.Name?.ToString(), FrozenNamespace, StringComparison.Ordinal)))
        {
            return root;
        }

        var frozenUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(FrozenNamespace))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        var insertIndex = 0;
        for (var i = 0; i < compilationUnit.Usings.Count; i++)
        {
            if (StringComparer.Ordinal.Compare(compilationUnit.Usings[i].Name?.ToString(), FrozenNamespace) < 0)
            {
                insertIndex = i + 1;
            }
        }

        return compilationUnit.WithUsings(compilationUnit.Usings.Insert(insertIndex, frozenUsing));
    }
}
