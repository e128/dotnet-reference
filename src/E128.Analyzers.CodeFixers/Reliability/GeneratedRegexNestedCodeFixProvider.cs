using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratedRegexNestedCodeFixProvider))]
[Shared]
public sealed class GeneratedRegexNestedCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GeneratedRegexAnalyzer.NestedQuantifierDiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not AttributeSyntax attribute
            || !RegexLiteralFix.TryGetPattern(attribute, out var pattern)
            || string.Equals(RewriteOuterQuantifier(pattern), pattern, StringComparison.Ordinal))
        {
            // This shape has no language-preserving one-token rewrite, so no action is offered.
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Fix the nested quantifier group", nameof(GeneratedRegexNestedCodeFixProvider), ct => RegexLiteralFix.ApplyRewriteAsync(context.Document, node, RewriteOuterQuantifier, ct));

        return Task.CompletedTask;
    }

    // A single-atom body makes the outer quantifier redundant, so the rewrite removes it
    // and the diagnostic clears. When removing the outer quantifier would lose the empty
    // match, like '(a+)*', the rewrite keeps it and makes the group non-capturing, so the
    // language stays. The diagnostic stays in that case. Any other body keeps the
    // language with an equivalent unbounded brace repeat. An unbounded brace outer
    // quantifier has no language-preserving one-token rewrite and stays flagged.
    internal static string RewriteOuterQuantifier(string pattern)
    {
        if (GeneratedRegexHelpers.FindNestedQuantifierSpan(pattern) is not { } span)
        {
            return pattern;
        }

        var quantifierIndex = span.QuantifierIndex;
        if (pattern[quantifierIndex] is '{')
        {
            return pattern;
        }

        if (span.SingleAtomBody)
        {
            if (RemovalKeepsLanguage(pattern, span))
            {
                return pattern.Remove(quantifierIndex, span.QuantifierLength);
            }

            return MakeGroupNonCapturing(pattern, span);
        }

        var lazy = span.QuantifierLength > 1 && pattern[quantifierIndex + 1] is '?';
        var replacement = string.Concat(pattern[quantifierIndex] is '*' ? "{0,}" : "{1,}", lazy ? "?" : string.Empty);
        return pattern.Substring(0, quantifierIndex) + replacement + pattern.Substring(quantifierIndex + span.QuantifierLength);
    }

    // Removing the outer quantifier keeps the language unless the outer quantifier is a
    // star and the single body atom requires at least one character: '(a*)*' and
    // '(a+)+' lose nothing, while '(a+)*' loses the empty match.
    private static bool RemovalKeepsLanguage(string pattern, GeneratedRegexHelpers.NestedQuantifierSpan span)
    {
        if (pattern[span.QuantifierIndex] is not '*')
        {
            return true;
        }

        var innerQuantifierIndex = span.CloseIndex - 1;
        if (pattern[innerQuantifierIndex] is '?')
        {
            // A lazy marker belongs to the inner atom quantifier.
            innerQuantifierIndex--;
        }

        return pattern[innerQuantifierIndex] is not '+';
    }

    // '(a+)*' becomes '(?:a+)*': the language keeps the empty match, and the group no
    // longer captures per iteration. An already non-capturing group returns unchanged,
    // so no fix is offered.
    private const int MarkerLength = 2;

    private static string MakeGroupNonCapturing(string pattern, GeneratedRegexHelpers.NestedQuantifierSpan span)
    {
        // '?:' spans two slots after the group open, so the second read steps two characters.
        if (pattern[span.OpenIndex + 1] is '?' && pattern[span.OpenIndex + MarkerLength] is ':')
        {
            return pattern;
        }

        return pattern.Substring(0, span.OpenIndex + 1) + "?:" + pattern.Substring(span.OpenIndex + 1);
    }
}
