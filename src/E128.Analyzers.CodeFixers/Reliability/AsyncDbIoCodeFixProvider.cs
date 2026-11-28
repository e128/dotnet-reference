using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
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
        RegisterNodeFix<InvocationExpressionSyntax>(
            context,
            root,
            "Await the async overload",
            nameof(AsyncDbIoCodeFixProvider),
            static invocation => invocation.Expression is MemberAccessExpressionSyntax
                                 && AsyncSiblingCodeFixHelper.HasFixableEnclosingMethod(invocation),
            (invocation, ct) => AsyncSiblingCodeFixHelper.ApplyFixAsync(context.Document, invocation, ct));

        return Task.CompletedTask;
    }
}
