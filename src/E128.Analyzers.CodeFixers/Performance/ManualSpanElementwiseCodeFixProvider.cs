using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128255: rewrites the element-wise loop as a <c lang="csharp">TensorPrimitives</c>
///     call.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualSpanElementwiseCodeFixProvider))]
[Shared]
public sealed class ManualSpanElementwiseCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ManualSpanElementwiseAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (node is not ForStatementSyntax forStatement)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (semanticModel is null
            || ManualSpanElementwiseAnalyzer.TryCreateContext(semanticModel.Compilation) is not { } elementwise
            || ManualSpanElementwiseAnalyzer.TryMatch(forStatement, semanticModel, elementwise, context.CancellationToken)
            is not { } match)
        {
            return;
        }

        CodeFixRegistration.Register(
            context,
            diagnostic,
            "Use TensorPrimitives." + match.Operation,
            nameof(ManualSpanElementwiseCodeFixProvider),
            cancellationToken => FixSyntax.ReplaceLoopAsync(
                context.Document,
                forStatement,
                SyntaxFactory.ParseStatement(BuildStatement(match)),
                "System.Numerics.Tensors",
                cancellationToken));
    }

    private static string BuildStatement(ManualSpanElementwiseAnalyzer.ElementwiseMatch match)
    {
        var first = match.First.ToString();
        var second = match.Second.ToString();
        var destination = match.Destination.ToString();

        return "TensorPrimitives." + match.Operation + "(" + first + ", " + second + ", " + destination + ");";
    }
}
