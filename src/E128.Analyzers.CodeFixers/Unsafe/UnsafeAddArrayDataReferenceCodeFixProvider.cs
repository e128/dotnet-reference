using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnsafeAddArrayDataReferenceCodeFixProvider))]
[Shared]
public sealed class UnsafeAddArrayDataReferenceCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnsafeAddArrayDataReferenceAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return Task.CompletedTask;
        }

        var access = UnsafeAddArrayDataReferenceAnalyzer.TryGetIndexedAccess(invocation);
        if (access is null)
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Index the array directly", nameof(UnsafeAddArrayDataReferenceCodeFixProvider), _ => Task.FromResult(ApplyFix(context.Document, root, invocation, access.Value)));

        return Task.CompletedTask;
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        (ExpressionSyntax Array, ExpressionSyntax Index) access)
    {
        var replacement = FixSyntax.IndexInto(access.Array, access.Index).WithTriviaFrom(invocation);
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }
}
