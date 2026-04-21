using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Style;

/// <summary>
///     Code fix for E128065: splits a bundled <c>#pragma warning disable ID1, ID2</c>
///     into separate single-ID pragmas so each can carry its own justification comment.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PragmaBundlingCodeFixProvider))]
[Shared]
public sealed class PragmaBundlingCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [PragmaBundlingAnalyzer.DiagnosticId];

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
        var trivia = root.FindTrivia(diagnostic.Location.SourceSpan.Start);
        if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Split into separate pragma directives",
                ct => SplitPragmaAsync(context.Document, diagnostic.Location, ct),
                nameof(PragmaBundlingCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> SplitPragmaAsync(
        Document document,
        Location location,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var trivia = root.FindTrivia(location.SourceSpan.Start);
        if (trivia.GetStructure() is not PragmaWarningDirectiveTriviaSyntax pragma)
        {
            return document;
        }

        var ids = new List<string>();
        foreach (var code in pragma.ErrorCodes)
        {
            var id = code.ToString().Trim();
            if (!string.IsNullOrEmpty(id))
            {
                ids.Add(id);
            }
        }

        if (ids.Count <= 1)
        {
            return document;
        }

        var replacements = new List<SyntaxTrivia>(ids.Count);
        foreach (var id in ids)
        {
            replacements.Add(SyntaxFactory.Trivia(BuildSingleIdPragma(id)));
        }

        var token = trivia.Token;
        SyntaxTriviaList originalList;
        bool isLeading;

        if (trivia.SpanStart < token.SpanStart)
        {
            originalList = token.LeadingTrivia;
            isLeading = true;
        }
        else
        {
            originalList = token.TrailingTrivia;
            isLeading = false;
        }

        var newList = ReplaceInTriviaList(originalList, trivia, replacements);
        var newToken = isLeading
            ? token.WithLeadingTrivia(newList)
            : token.WithTrailingTrivia(newList);
        var newRoot = root.ReplaceToken(token, newToken);
        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxTriviaList ReplaceInTriviaList(
        SyntaxTriviaList list,
        SyntaxTrivia target,
        List<SyntaxTrivia> replacements)
    {
        var result = new List<SyntaxTrivia>(list.Count - 1 + replacements.Count);
        foreach (var t in list)
        {
            if (t.SpanStart == target.SpanStart && t.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
            {
                result.AddRange(replacements);
            }
            else
            {
                result.Add(t);
            }
        }

        return SyntaxFactory.TriviaList(result);
    }

    private static PragmaWarningDirectiveTriviaSyntax BuildSingleIdPragma(string diagnosticId)
    {
        return SyntaxFactory.PragmaWarningDirectiveTrivia(
            SyntaxFactory.Token(SyntaxKind.HashToken),
            SyntaxFactory.Token(SyntaxKind.PragmaKeyword).WithLeadingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.WarningKeyword).WithLeadingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.DisableKeyword).WithLeadingTrivia(SyntaxFactory.Space),
            SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                SyntaxFactory.IdentifierName(diagnosticId).WithLeadingTrivia(SyntaxFactory.Space)),
            SyntaxFactory.Token(SyntaxKind.EndOfDirectiveToken).WithTrailingTrivia(SyntaxFactory.LineFeed),
            true);
    }
}
