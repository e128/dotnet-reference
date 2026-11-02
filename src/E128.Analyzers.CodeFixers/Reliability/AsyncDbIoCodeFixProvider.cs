using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncDbIoCodeFixProvider))]
[Shared]
public sealed class AsyncDbIoCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [AsyncDbIoAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax
            || !AsyncSiblingCodeFixHelper.HasFixableEnclosingMethod(invocation))
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Await the async overload",
                ct => AsyncSiblingCodeFixHelper.ApplyFixAsync(context.Document, invocation, ct),
                nameof(AsyncDbIoCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }
}
