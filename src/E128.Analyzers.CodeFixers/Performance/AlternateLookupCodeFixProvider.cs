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
///     Code fix for E128133: replaces the temporary string key with a span key read through
///     <c lang="csharp">GetAlternateLookup&lt;ReadOnlySpan&lt;char&gt;&gt;()</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AlternateLookupCodeFixProvider))]
[Shared]
public sealed class AlternateLookupCodeFixProvider : E128CodeFixProviderBase
{
    private const string Lookup = ".GetAlternateLookup<ReadOnlySpan<char>>()";

    public override ImmutableArray<string> FixableDiagnosticIds => [AlternateLookupAnalyzer.DiagnosticId];

    /// <summary>
    ///     Fix All stays off. Each fix rewrites one receiver expression, and a file can mix
    ///     ordinal and culture aware collections.
    /// </summary>
    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var creation = root.FindNode(context.Diagnostics[0].Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<ObjectCreationExpressionSyntax>()
            .FirstOrDefault();

        if (creation?.ArgumentList is not { Arguments.Count: 1 } argumentList
            || creation.Parent is not ArgumentSyntax { Parent.Parent: { } owner })
        {
            return Task.CompletedTask;
        }

        var span = argumentList.Arguments[0].Expression;

        var replacement = owner switch
        {
            ElementAccessExpressionSyntax access => BuildElementAccess(access, span),
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation
                => BuildInvocation(invocation, member, span),
            _ => null
        };

        if (replacement is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use GetAlternateLookup<ReadOnlySpan<char>>",
                ct => ReplaceAsync(context.Document, owner, replacement, ct),
                AlternateLookupAnalyzer.DiagnosticId),
            context.Diagnostics[0]);

        return Task.CompletedTask;
    }

    private static ExpressionSyntax? BuildElementAccess(
        ElementAccessExpressionSyntax access,
        ExpressionSyntax span)
    {
        return access.Expression is IdentifierNameSyntax receiverName
            ? SyntaxFactory.ParseExpression(
                receiverName.ToFullString() + Lookup + "[" + span.WithoutTrivia().ToFullString() + "]")
            : null;
    }

    private static ExpressionSyntax? BuildInvocation(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        ExpressionSyntax span)
    {
        var arguments = invocation.ArgumentList.Arguments
            .Select((argument, index) => index == 0 ? span.WithoutTrivia().ToFullString() : argument.ToFullString());

        return member.Expression is IdentifierNameSyntax receiverName
            ? SyntaxFactory.ParseExpression(
                receiverName.ToFullString() + Lookup + "." + member.Name.ToFullString()
                + "(" + string.Join(", ", arguments) + ")")
            : null;
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        SyntaxNode owner,
        ExpressionSyntax replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newRoot = root.ReplaceNode(owner, replacement.WithTriviaFrom(owner));
        return document.WithSyntaxRoot(UsingDirectives.AddIfMissing(newRoot, "System"));
    }
}
