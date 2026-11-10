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
///     Code fix for E128134: replaces the bit-count loop with one
///     <c lang="csharp">BitOperations.PopCount</c> call.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualBitCountCodeFixProvider))]
[Shared]
public sealed class ManualBitCountCodeFixProvider : E128CodeFixProviderBase
{
    private const string NumericsNamespace = "System.Numerics";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ManualBitCountAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
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

        var valueIdentifier = SyntaxFactory.IdentifierName(valueName);
        ExpressionSyntax operand = valueIdentifier;
        if (valueType.SpecialType is SpecialType.System_Int32)
        {
            operand = SyntaxFactory.CastExpression(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.UIntKeyword)), valueIdentifier);
        }
        else if (valueType.SpecialType is SpecialType.System_Int64)
        {
            operand = SyntaxFactory.CastExpression(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ULongKeyword)), valueIdentifier);
        }

        var replacement = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.AddAssignmentExpression,
                SyntaxFactory.IdentifierName(countName),
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("BitOperations"),
                        SyntaxFactory.IdentifierName("PopCount")),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(operand))))));

        CodeFixRegistration.Register(context, context.Diagnostics[0], "Use BitOperations.PopCount", ManualBitCountAnalyzer.DiagnosticId, ct => FixSyntax.ReplaceLoopAsync(context.Document, loop, replacement, NumericsNamespace, ct));
    }
}
