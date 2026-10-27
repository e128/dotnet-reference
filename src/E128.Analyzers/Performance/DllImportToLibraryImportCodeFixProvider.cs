using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DllImportToLibraryImportCodeFixProvider))]
[Shared]
public sealed class DllImportToLibraryImportCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DllImportToLibraryImportAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
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

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not AttributeSyntax attribute
            || attribute.Parent?.Parent is not MethodDeclarationSyntax method)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model?.GetDeclaredSymbol(method, context.CancellationToken) is not IMethodSymbol methodSymbol
            || !DllImportToLibraryImportAnalyzer.IsBlittableSignature(methodSymbol)
            || model.Compilation.Options is not CSharpCompilationOptions { AllowUnsafe: true }
            || !HasOnlyTranslatableArguments(attribute))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use LibraryImport",
                _ => Task.FromResult(ApplyFix(context.Document, root, attribute, method)),
                nameof(DllImportToLibraryImportCodeFixProvider)),
            diagnostic);
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        AttributeSyntax attribute,
        MethodDeclarationSyntax method)
    {
        var libraryImport = SyntaxFactory.IdentifierName("LibraryImport");
        NameSyntax newName = attribute.Name is QualifiedNameSyntax qualified
            ? qualified.WithRight(libraryImport.WithTriviaFrom(qualified.Right))
            : libraryImport.WithTriviaFrom(attribute.Name);
        var renamedAttribute = attribute.WithName(newName);

        var newMethod = method.ReplaceNode(attribute, renamedAttribute);
        newMethod = MakePartial(newMethod);

        var targets = new List<SyntaxNode> { method };
        targets.AddRange(method.Ancestors().OfType<TypeDeclarationSyntax>());

        var newRoot = root.ReplaceNodes(
            targets,
            (original, rewritten) => original == method
                ? newMethod
                : MakeTypePartial((TypeDeclarationSyntax)rewritten));

        return document.WithSyntaxRoot(newRoot);
    }

    // LibraryImport has no CharSet, CallingConvention, PreserveSig, ExactSpelling or BestFitMapping.
    private static bool HasOnlyTranslatableArguments(AttributeSyntax attribute)
    {
        return attribute.ArgumentList is null
            || attribute.ArgumentList.Arguments.All(argument => argument.NameEquals is null
                || argument.NameEquals.Name.Identifier.ValueText is "EntryPoint" or "SetLastError");
    }

    private static MethodDeclarationSyntax MakePartial(MethodDeclarationSyntax method)
    {
        var modifiers = method.Modifiers;
        var externIndex = -1;

        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.ExternKeyword))
            {
                externIndex = i;
                break;
            }
        }

        if (externIndex >= 0)
        {
            modifiers = modifiers.RemoveAt(externIndex);
        }

        return HasModifier(modifiers, SyntaxKind.PartialKeyword)
            ? method.WithModifiers(modifiers)
            : method.WithModifiers(modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword)));
    }

    private static TypeDeclarationSyntax MakeTypePartial(TypeDeclarationSyntax type)
    {
        return HasModifier(type.Modifiers, SyntaxKind.PartialKeyword)
            ? type
            : type.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
    }

    private static bool HasModifier(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        foreach (var modifier in modifiers)
        {
            if (modifier.IsKind(kind))
            {
                return true;
            }
        }

        return false;
    }
}
