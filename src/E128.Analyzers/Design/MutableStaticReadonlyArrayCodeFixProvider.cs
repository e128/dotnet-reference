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

namespace E128.Analyzers.Design;

/// <summary>
/// Code fix for E128061: replaces <c>static readonly T[]</c> with <c>ImmutableArray&lt;T&gt;</c>
/// and converts the initializer to a collection expression.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MutableStaticReadonlyArrayCodeFixProvider))]
[Shared]
public sealed class MutableStaticReadonlyArrayCodeFixProvider : CodeFixProvider
{
    private const string ImmutableNamespace = "System.Collections.Immutable";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MutableStaticReadonlyArrayAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                title: "Convert to ImmutableArray<T>",
                createChangedDocument: ct => ConvertToImmutableArrayAsync(context.Document, declaration, ct),
                equivalenceKey: MutableStaticReadonlyArrayAnalyzer.DiagnosticId),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ConvertToImmutableArrayAsync(
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

        // Get the element type from the array type
        if (declaration.Type is not ArrayTypeSyntax arrayType)
        {
            return document;
        }

        var elementTypeSyntax = arrayType.ElementType;

        // Build ImmutableArray<T> type syntax
        var immutableArrayType = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("ImmutableArray"),
            SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList([elementTypeSyntax])));

        // Transform initializers: convert new T[] { ... } / new[] { ... } to collection expressions
        var newVariables = declaration.Variables.Select(TransformVariable).ToList();

        var newDeclaration = declaration
            .WithType(immutableArrayType.WithTriviaFrom(arrayType))
            .WithVariables(SyntaxFactory.SeparatedList(newVariables));

        var newRoot = AddUsingIfMissing(root.ReplaceNode(declaration, newDeclaration));
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Converts the variable initializer to a collection expression if needed.
    /// Collection expressions (["a", "b"]) work directly with ImmutableArray.
    /// Explicit array construction (new T[] { ... } or new[] { ... }) is unwrapped to collection expressions.
    /// </summary>
    private static VariableDeclaratorSyntax TransformVariable(VariableDeclaratorSyntax variable)
    {
        if (variable.Initializer is null)
        {
            return variable;
        }

        var expression = variable.Initializer.Value;

        // Collection expression ["a", "b"] → works as-is with ImmutableArray<T>
        if (expression is CollectionExpressionSyntax)
        {
            return variable;
        }

        // new T[] { "a", "b" } or new[] { "a", "b" } → unwrap to collection expression
        var unwrapped = UnwrapArrayCreation(expression);
        return unwrapped is not null
            ? variable.WithInitializer(variable.Initializer.WithValue(unwrapped))
            : variable;
    }

    /// <summary>
    /// Unwraps array creation expressions to collection expressions.
    /// new T[] { "a", "b" } → ["a", "b"]
    /// new[] { "a", "b" } → ["a", "b"]
    /// </summary>
    private static CollectionExpressionSyntax? UnwrapArrayCreation(ExpressionSyntax expression)
    {
        // Implicit: new[] { "a", "b" } or Explicit: new T[] { "a", "b" }
        var initializer = expression is ImplicitArrayCreationExpressionSyntax implicitArray
            ? implicitArray.Initializer
            : expression is ArrayCreationExpressionSyntax explicitArray
                ? explicitArray.Initializer
                : null;

        return initializer is not null
            ? CollectionExpressionFromInitializers(initializer)
            : null;
    }

    private static CollectionExpressionSyntax? CollectionExpressionFromInitializers(InitializerExpressionSyntax? initializer)
    {
        if (initializer is null || !initializer.Expressions.Any())
        {
            return null;
        }

        var elements = initializer.Expressions.Select(expr =>
            SyntaxFactory.ExpressionElement(expr.WithoutTrailingTrivia())).ToList();

        return SyntaxFactory.CollectionExpression(
            SyntaxFactory.SeparatedList<CollectionElementSyntax>(elements));
    }

    private static SyntaxNode AddUsingIfMissing(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        if (compilationUnit.Usings.Any(u =>
            string.Equals(u.Name?.ToString(), ImmutableNamespace, System.StringComparison.Ordinal)))
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ImmutableNamespace))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        var insertIndex = 0;
        for (var i = 0; i < compilationUnit.Usings.Count; i++)
        {
            if (System.StringComparer.Ordinal.Compare(compilationUnit.Usings[i].Name?.ToString(), ImmutableNamespace) < 0)
            {
                insertIndex = i + 1;
            }
        }

        return compilationUnit.WithUsings(compilationUnit.Usings.Insert(insertIndex, usingDirective));
    }
}
