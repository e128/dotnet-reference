using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Security;

/// <summary>
///     Code fix for E128157: replaces the comparison with
///     <c lang="csharp">CryptographicOperations.FixedTimeEquals</c>. A negated form becomes a
///     logical negation of the fixed-time result.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TimingSafeComparisonCodeFixProvider))]
[Shared]
public sealed class TimingSafeComparisonCodeFixProvider : E128CodeFixProviderBase
{
    private const string FixedTimeEqualsExpression = "CryptographicOperations.FixedTimeEquals";

    private const string OperationsTypeName = "System.Security.Cryptography.CryptographicOperations";

    private const string FixedTimeEqualsMethodName = "FixedTimeEquals";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TimingSafeComparisonAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (node.FirstAncestorOrSelf<ExpressionSyntax>(candidate =>
                candidate.Span == diagnostic.Location.SourceSpan) is not { } expression)
        {
            return;
        }

        var arguments = expression switch
        {
            BinaryExpressionSyntax binary => GetBinaryArguments(binary),
            InvocationExpressionSyntax invocation => GetSequenceEqualArguments(invocation),
            _ => null
        };

        if (arguments is null
            || !await HasFixedTimeEqualsAsync(context.Document, context.CancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var replacement = expression is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.NotEqualsExpression }
            ? $"!{FixedTimeEqualsExpression}({arguments})"
            : $"{FixedTimeEqualsExpression}({arguments})";

        context.RegisterCodeFix(
            CodeAction.Create(
                "Compare in fixed time",
                ct => ReplaceAsync(context.Document, expression, replacement, ct),
                nameof(TimingSafeComparisonCodeFixProvider)),
            diagnostic);
    }

    /// <summary>
    ///     True when the target framework ships
    ///     <c lang="csharp">CryptographicOperations.FixedTimeEquals</c>. The type appears in .NET Core
    ///     2.1 and .NET Standard 2.1, so the fix stays absent for an older target.
    /// </summary>
    private static async Task<bool> HasFixedTimeEqualsAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

        return compilation?.GetTypeByMetadataName(OperationsTypeName)
            ?.GetMembers(FixedTimeEqualsMethodName).Length > 0;
    }

    private static string GetBinaryArguments(BinaryExpressionSyntax binary)
    {
        var leftText = binary.Left.ToString();
        var rightText = binary.Right.ToString();

        return $"{leftText}, {rightText}";
    }

    private static string? GetSequenceEqualArguments(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return null;
        }

        var receiverText = memberAccess.Expression.ToString();
        var argumentText = invocation.ArgumentList.Arguments[0].Expression.ToString();

        return $"{receiverText}, {argumentText}";
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        ExpressionSyntax expression,
        string replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        var newExpression = SyntaxFactory.ParseExpression(replacement)
            .WithTriviaFrom(expression);

        var newRoot = root.ReplaceNode(expression, newExpression);
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.Security.Cryptography");

        return document.WithSyntaxRoot(newRoot);
    }
}
