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
///     Code fix for E128065: splits a bundled <c lang="csharp">#pragma warning disable ID1, ID2</c>
///     into separate single-ID pragmas so each can carry its own justification comment.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PragmaBundlingCodeFixProvider))]
[Shared]
public sealed class PragmaBundlingCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [PragmaBundlingAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var trivia = root.FindTrivia(diagnostic.Location.SourceSpan.Start);
        if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Split into separate pragma directives",
                ct => SplitPragmaAsync(context.Document, diagnostic.Location, ct),
                nameof(PragmaBundlingCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
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

        var ids = CollectIds(pragma);
        if (ids.Count <= 1)
        {
            return document;
        }

        var replacements = BuildReplacements(pragma, ids);
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

    private static List<PragmaWarningDirectiveTriviaSyntax> BuildReplacements(
        PragmaWarningDirectiveTriviaSyntax pragma,
        List<string> ids)
    {
        var directives = new List<PragmaWarningDirectiveTriviaSyntax>(ids.Count);
        foreach (var id in ids)
        {
            directives.Add(BuildSingleIdPragma(id));
        }

        var tail = CollectTail(pragma);
        if (tail.Count > 0)
        {
            var last = directives[directives.Count - 1];
            var code = last.ErrorCodes.Last();
            directives[directives.Count - 1] = last.WithErrorCodes(
                last.ErrorCodes.Replace(code, code.WithTrailingTrivia(tail)));
        }

        return directives;
    }

    // Everything after the last ID stays with the last line of the split: the separator space and
    // any justification comment. The generated directive supplies its own line break.
    private static SyntaxTriviaList CollectTail(PragmaWarningDirectiveTriviaSyntax pragma)
    {
        var tail = new List<SyntaxTrivia>();
        tail.AddRange(pragma.ErrorCodes.Last().GetTrailingTrivia());
        tail.AddRange(pragma.EndOfDirectiveToken.LeadingTrivia);

        var kept = new List<SyntaxTrivia>(tail.Count);
        foreach (var trivia in tail)
        {
            if (!trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                kept.Add(trivia);
            }
        }

        return SyntaxFactory.TriviaList(kept);
    }

    private static List<string> CollectIds(PragmaWarningDirectiveTriviaSyntax pragma)
    {
        var ids = new List<string>();
        foreach (var code in pragma.ErrorCodes)
        {
            var id = code.ToString().Trim();
            if (!string.IsNullOrEmpty(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static SyntaxTriviaList ReplaceInTriviaList(
        SyntaxTriviaList list,
        SyntaxTrivia target,
        List<PragmaWarningDirectiveTriviaSyntax> replacements)
    {
        var result = new List<SyntaxTrivia>(list.Count);
        foreach (var t in list)
        {
            if (!t.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia) || t.SpanStart != target.SpanStart)
            {
                result.Add(t);
                continue;
            }

            foreach (var directive in replacements)
            {
                result.Add(SyntaxFactory.Trivia(directive));
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
