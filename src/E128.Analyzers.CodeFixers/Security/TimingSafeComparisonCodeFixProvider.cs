using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Security;

/// <summary>
///     Code fix for E128157: replaces a <c lang="csharp">SequenceEqual</c> call with
///     <c lang="csharp">CryptographicOperations.FixedTimeEquals</c>. The fix declines when an operand
///     may be null, because <c lang="csharp">FixedTimeEquals</c> reads a null array as empty and
///     <c lang="csharp">SequenceEqual</c> throws. A string comparison has no fix, because
///     <c lang="csharp">FixedTimeEquals</c> takes bytes.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TimingSafeComparisonCodeFixProvider))]
[Shared]
public sealed class TimingSafeComparisonCodeFixProvider : E128CodeFixProviderBase
{
    private const string OperationsTypeName = "System.Security.Cryptography.CryptographicOperations";

    private const string FixedTimeEqualsMethodName = "FixedTimeEquals";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TimingSafeComparisonAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (node.FirstAncestorOrSelf<InvocationExpressionSyntax>(candidate =>
                candidate.Span == diagnostic.Location.SourceSpan) is not { } invocation
            || GetSequenceEqualOperands(invocation) is not { } operands)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (model is null
            || !HasFixedTimeEquals(model)
            || !AreNonNull(model, operands, context.CancellationToken))
        {
            return;
        }

        var replacement = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("CryptographicOperations"),
                SyntaxFactory.IdentifierName(FixedTimeEqualsMethodName)),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList([
                    SyntaxFactory.Argument(operands.Left.WithoutTrivia()),
                    SyntaxFactory.Argument(operands.Right.WithoutTrivia())
                ])));

        CodeFixRegistration.Register(context, diagnostic, "Compare in fixed time", nameof(TimingSafeComparisonCodeFixProvider), ct => ReplaceAsync(context.Document, invocation, replacement, ct));
    }

    /// <summary>
    ///     True when the target framework ships
    ///     <c lang="csharp">CryptographicOperations.FixedTimeEquals</c>. The type appears in .NET Core
    ///     2.1 and .NET Standard 2.1, so the fix stays absent for an older target.
    /// </summary>
    private static bool HasFixedTimeEquals(SemanticModel model)
    {
        return model.Compilation.GetTypeByMetadataName(OperationsTypeName)
            ?.GetMembers(FixedTimeEqualsMethodName).Length > 0;
    }

    /// <summary>
    ///     True when the flow analysis does not report either operand as possibly null. A project
    ///     without nullable annotations reports no flow state, so the fix stays available there.
    /// </summary>
    private static bool AreNonNull(
        SemanticModel model,
        (ExpressionSyntax Left, ExpressionSyntax Right) operands,
        CancellationToken cancellationToken)
    {
        return model.GetTypeInfo(operands.Left, cancellationToken).Nullability.FlowState
               != NullableFlowState.MaybeNull
               && model.GetTypeInfo(operands.Right, cancellationToken).Nullability.FlowState
               != NullableFlowState.MaybeNull;
    }

    private static (ExpressionSyntax Left, ExpressionSyntax Right)? GetSequenceEqualOperands(
        InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;

        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
               && string.Equals(
                   memberAccess.Name.Identifier.ValueText,
                   "SequenceEqual",
                   StringComparison.Ordinal)
            ? arguments.Count switch
            {
                1 => (memberAccess.Expression, arguments[0].Expression),
                2 => (arguments[0].Expression, arguments[1].Expression),
                _ => null
            }
            : null;
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        ExpressionSyntax expression,
        ExpressionSyntax replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        var newExpression = replacement.WithTriviaFrom(expression);

        var newRoot = root.ReplaceNode(expression, newExpression);
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.Security.Cryptography");

        return document.WithSyntaxRoot(newRoot);
    }
}
