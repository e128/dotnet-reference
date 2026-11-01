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
///     Code fix for E128131: passes the UTF-8 bytes to the span overload of
///     <c lang="csharp">Parse</c> or <c lang="csharp">TryParse</c> and drops the intermediate
///     string.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Utf8StringParseCodeFixProvider))]
[Shared]
public sealed class Utf8StringParseCodeFixProvider : CodeFixProvider
{
    private const string GlobalizationNamespace = "System.Globalization";
    private const string CurrentCultureExpression = "CultureInfo.CurrentCulture";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [Utf8StringParseAnalyzer.DiagnosticId];

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

        var diagnostic = context.Diagnostics[0];

        var getString = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();

        if (getString?.ArgumentList is null
            || getString.ArgumentList.Arguments.Count != 1
            || getString.Parent is not ArgumentSyntax argument
            || argument.Parent?.Parent is not InvocationExpressionSyntax parseInvocation)
        {
            return;
        }

        var addProvider = diagnostic.Properties.TryGetValue(Utf8StringParseAnalyzer.AddProviderKey, out var value)
            && string.Equals(value, "true", System.StringComparison.Ordinal);

        context.RegisterCodeFix(
            CodeAction.Create(
                "Pass the UTF-8 bytes",
                ct => RewriteAsync(context.Document, argument, getString, parseInvocation, addProvider, ct),
                Utf8StringParseAnalyzer.DiagnosticId),
            diagnostic);
    }

    private static async Task<Document> RewriteAsync(
        Document document,
        ArgumentSyntax argument,
        InvocationExpressionSyntax getString,
        InvocationExpressionSyntax parseInvocation,
        bool addProvider,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || getString.ArgumentList is null)
        {
            return document;
        }

        var bytes = getString.ArgumentList.Arguments[0].Expression;

        var newArgument = argument.WithExpression(
            bytes.WithoutTrivia().WithTriviaFrom(argument.Expression));

        var newInvocation = parseInvocation.ReplaceNode(argument, newArgument);

        if (addProvider && newInvocation.ArgumentList is { } list)
        {
            newInvocation = newInvocation.WithArgumentList(
                list.AddArguments(SyntaxFactory.Argument(SyntaxFactory.ParseExpression(CurrentCultureExpression))));
        }

        var newRoot = root.ReplaceNode(parseInvocation, newInvocation);

        if (addProvider)
        {
            newRoot = UsingDirectives.AddIfMissing(newRoot, GlobalizationNamespace);
        }

        return document.WithSyntaxRoot(newRoot);
    }
}
