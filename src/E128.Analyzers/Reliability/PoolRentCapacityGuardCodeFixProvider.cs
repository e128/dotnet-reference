using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128070: wraps the <c lang="csharp">Rent</c> argument in
///     <c lang="csharp">Math.Min</c> so a large request cannot make the pool allocate an
///     unbounded buffer. The cap comes from the <c lang="text">e128_pool_rent_max_capacity</c> option.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PoolRentCapacityGuardCodeFixProvider))]
[Shared]
public sealed class PoolRentCapacityGuardCodeFixProvider : CodeFixProvider
{
    internal const string MaxRentCapacityOptionKey = "e128_pool_rent_max_capacity";

    internal const int DefaultMaxRentCapacity = 1_048_576;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [PoolRentCapacityGuardAnalyzer.DiagnosticId];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var invocation = node as InvocationExpressionSyntax ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>();

        if (invocation is null || invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0];
        var argumentText = argument.Expression.ToString();
        var capText = FormatCapacity(ReadCapacity(context.Document, root.SyntaxTree));
        var guardText = $"Math.Min({argumentText}, {capText})";

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Guard with {guardText}",
                ct => GuardAsync(context.Document, invocation, argument, guardText, ct),
                nameof(PoolRentCapacityGuardCodeFixProvider)),
            diagnostic);
    }

    private static int ReadCapacity(Document document, SyntaxTree syntaxTree)
    {
        var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);

        return options.TryGetValue(MaxRentCapacityOptionKey, out var rawValue)
               && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var capacity)
               && capacity > 0
            ? capacity
            : DefaultMaxRentCapacity;
    }

    // Digit separators keep a six-figure cap readable in the emitted call.
    private static string FormatCapacity(int capacity)
    {
        return capacity.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '_');
    }

    private static async Task<Document> GuardAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ArgumentSyntax argument,
        string guardText,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacementArgument = argument
            .WithExpression(SyntaxFactory.ParseExpression(guardText))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newInvocation = invocation.WithArgumentList(invocation.ArgumentList.WithArguments(
            invocation.ArgumentList.Arguments.Replace(argument, replacementArgument)));

        var newRoot = root.ReplaceNode(invocation, newInvocation);
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System");

        return document.WithSyntaxRoot(newRoot);
    }
}
