using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128135: replaces the sum loop with one
///     <c lang="csharp">TensorPrimitives.Sum</c> call.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualSpanSumCodeFixProvider))]
[Shared]
public sealed class ManualSpanSumCodeFixProvider : E128CodeFixProviderBase
{
    private const string TensorsNamespace = "System.Numerics.Tensors";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ManualSpanSumAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var loop = root.FindNode(context.Diagnostics[0].Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<ForEachStatementSyntax>()
            .FirstOrDefault();

        if (loop is null || !ManualSpanSumAnalyzer.TryMatchLoop(loop, out var sumName, out _))
        {
            return Task.CompletedTask;
        }

        var replacement = SyntaxFactory.ParseStatement(
            sumName + " += TensorPrimitives.Sum(" + loop.Expression.WithoutTrivia().ToFullString() + ");");

        CodeFixRegistration.Register(context, context.Diagnostics[0], "Use TensorPrimitives.Sum", ManualSpanSumAnalyzer.DiagnosticId, ct => FixSyntax.ReplaceLoopAsync(context.Document, loop, replacement, TensorsNamespace, ct));

        return Task.CompletedTask;
    }
}
