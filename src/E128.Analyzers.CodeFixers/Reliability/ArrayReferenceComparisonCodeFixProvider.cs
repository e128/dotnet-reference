using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128238: rewrites an array comparison to
///     <c lang="csharp">Enumerable.SequenceEqual</c> and adds
///     <c lang="csharp">using System.Linq;</c> when absent.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ArrayReferenceComparisonCodeFixProvider))]
[Shared]
public sealed class ArrayReferenceComparisonCodeFixProvider : E128CodeFixProviderBase
{
    private const int StaticEqualsArgumentCount = 2;

    private const int InstanceEqualsArgumentCount = 1;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ArrayReferenceComparisonAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null || TryBuildReplacement(node, semanticModel, context) is not { } replacement)
        {
            return;
        }

        var newRoot = root.ReplaceNode(replacement.Original, replacement.Replacement);
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.Linq");

        CodeFixRegistration.Register(
            context,
            diagnostic,
            "Compare array elements with SequenceEqual",
            nameof(ArrayReferenceComparisonCodeFixProvider),
            _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)));
    }

    private static (SyntaxNode Original, ExpressionSyntax Replacement)? TryBuildReplacement(
        SyntaxNode node,
        SemanticModel semanticModel,
        CodeFixContext context)
    {
        return node is BinaryExpressionSyntax binary
            ? TryBuildBinary(binary)
            : node is InvocationExpressionSyntax invocation
                ? TryBuildInvocation(invocation, semanticModel, context)
                : null;
    }

    private static (SyntaxNode Original, ExpressionSyntax Replacement)? TryBuildBinary(BinaryExpressionSyntax binary)
    {
        if (!binary.IsKind(SyntaxKind.EqualsExpression) && !binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return null;
        }

        var call = SequenceEqual(binary.Left, binary.Right);
        ExpressionSyntax replacement = binary.IsKind(SyntaxKind.NotEqualsExpression)
            ? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, call)
            : call;

        return (binary, replacement.WithTriviaFrom(binary));
    }

    private static (SyntaxNode Original, ExpressionSyntax Replacement)? TryBuildInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CodeFixContext context)
    {
        if (semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !string.Equals(method.Name, "Equals", System.StringComparison.Ordinal))
        {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (method.IsStatic)
        {
            return arguments.Count is StaticEqualsArgumentCount
                ? (invocation, SequenceEqual(arguments[0].Expression, arguments[1].Expression).WithTriviaFrom(invocation))
                : null;
        }

        if (arguments.Count is not InstanceEqualsArgumentCount
            || invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return null;
        }

        return (invocation, SequenceEqual(access.Expression, arguments[0].Expression).WithTriviaFrom(invocation));
    }

    private static InvocationExpressionSyntax SequenceEqual(ExpressionSyntax left, ExpressionSyntax right)
    {
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("Enumerable"),
                SyntaxFactory.IdentifierName("SequenceEqual")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(
                [
                    SyntaxFactory.Argument(left.WithoutTrivia()),
                    SyntaxFactory.Argument(right.WithoutTrivia())
                ])));
    }
}
