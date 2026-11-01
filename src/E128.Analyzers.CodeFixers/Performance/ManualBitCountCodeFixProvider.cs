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
///     Code fix for E128134: replaces the bit-count loop with one
///     <c lang="csharp">BitOperations.PopCount</c> call.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualBitCountCodeFixProvider))]
[Shared]
public sealed class ManualBitCountCodeFixProvider : CodeFixProvider
{
    private const string NumericsNamespace = "System.Numerics";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ManualBitCountAnalyzer.DiagnosticId];

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

        var loop = root.FindNode(context.Diagnostics[0].Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<WhileStatementSyntax>()
            .FirstOrDefault();

        if (loop is null || !ManualBitCountAnalyzer.TryMatchLoop(loop, out var valueName, out var countName))
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (semanticModel is null
            || ManualBitCountAnalyzer.FindType(semanticModel, loop, valueName, context.CancellationToken)
                is not { } valueType)
        {
            return;
        }

        var operand = valueType.SpecialType == SpecialType.System_Int32
            ? "(uint)" + valueName
            : valueType.SpecialType == SpecialType.System_Int64
                ? "(ulong)" + valueName
                : valueName;

        var replacement = SyntaxFactory.ParseStatement(
            countName + " += BitOperations.PopCount(" + operand + ");");

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use BitOperations.PopCount",
                ct => ReplaceLoopAsync(context.Document, loop, replacement, ct),
                ManualBitCountAnalyzer.DiagnosticId),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ReplaceLoopAsync(
        Document document,
        WhileStatementSyntax loop,
        StatementSyntax replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newRoot = root.ReplaceNode(loop, replacement.WithTriviaFrom(loop));
        return document.WithSyntaxRoot(UsingDirectives.AddIfMissing(newRoot, NumericsNamespace));
    }
}
