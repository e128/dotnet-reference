using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Testing;

/// <summary>
///     Code fix for E128107 and E128108: removes the whole test method. A low-value test that locks an
///     implementation detail in place is deleted rather than rewritten, so the fix removes the method
///     declaration and leaves the surrounding members untouched.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LowValueTestCodeFixProvider))]
[Shared]
public sealed class LowValueTestCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [LowValueTestAnalyzer.SuggestionDiagnosticId, LowValueTestAnalyzer.WarningDiagnosticId];

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
        var span = diagnostic.Location.SourceSpan;

        // The fix is scoped to a test method. The rule anchors every diagnostic on the method name, so a
        // diagnostic on any other node, or on a method with no xUnit attribute, registers no action.
        var method = root.FindNode(span).FirstAncestorOrSelf<MethodDeclarationSyntax>();

        if (method is null || XunitTestHelper.GetFactOrTheoryAttribute(method) is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Delete the low-value test method",
                ct => DeleteMethodAsync(context.Document, span, ct),
                nameof(LowValueTestCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> DeleteMethodAsync(
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var method = root.FindNode(span).FirstAncestorOrSelf<MethodDeclarationSyntax>();

        return method is null
            ? document
            : document.WithSyntaxRoot(RemoveMethod(root, method));
    }

    private static SyntaxNode RemoveMethod(SyntaxNode root, MethodDeclarationSyntax method)
    {
        return IsTrailingMember(method)
            ? TrimTrailingMember(root, method)
            : root.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia)!;
    }

    private static bool IsTrailingMember(MethodDeclarationSyntax method)
    {
        return method.Parent is TypeDeclarationSyntax type
               && type.Members.Count > 1
               && type.Members.Last() == method;
    }

    private static SyntaxNode TrimTrailingMember(SyntaxNode root, MethodDeclarationSyntax method)
    {
        // A plain removal of the last member leaves the blank line that separated it from the member
        // above. Trim that line in the same rewrite.
        var type = (TypeDeclarationSyntax)method.Parent!;
        var withoutMethod = type.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia)!;

        return root.ReplaceNode(
            type,
            withoutMethod.WithCloseBraceToken(
                withoutMethod.CloseBraceToken.WithLeadingTrivia(default(SyntaxTriviaList))));
    }
}
