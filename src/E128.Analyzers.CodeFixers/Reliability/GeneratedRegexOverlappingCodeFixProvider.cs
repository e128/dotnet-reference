using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratedRegexOverlappingCodeFixProvider))]
[Shared]
public sealed class GeneratedRegexOverlappingCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GeneratedRegexAnalyzer.OverlappingQuantifierDiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not AttributeSyntax attribute
            || !RegexLiteralFix.TryGetPattern(attribute, out var pattern)
            || string.Equals(RemoveOverlappingWhitespaceQuantifier(pattern), pattern, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Remove overlapping \\s quantifier", nameof(GeneratedRegexOverlappingCodeFixProvider), ct => RegexLiteralFix.ApplyRewriteAsync(context.Document, node, RemoveOverlappingWhitespaceQuantifier, ct));

        return Task.CompletedTask;
    }

    // Detection mirrors the analyzer scan, so the rewrite targets the element the
    // diagnostic reports. A required '\s+' collapses to a bare '\s', which keeps the
    // separator. An optional '\s*' goes away with its whole token.
    internal static string RemoveOverlappingWhitespaceQuantifier(string pattern)
    {
        var inClass = GeneratedRegexHelpers.BuildCharacterClassMask(pattern);
        for (var i = 0; i < pattern.Length - GeneratedRegexHelpers.WhitespaceEscapeLength; i++)
        {
            if (inClass[i])
            {
                continue;
            }

            var quantifierEnd = GeneratedRegexHelpers.WhitespaceQuantifierEnd(pattern, i);
            if (quantifierEnd < 0)
            {
                continue;
            }

            if (GeneratedRegexHelpers.HasOverlappingElementAfter(pattern, quantifierEnd)
                || GeneratedRegexHelpers.HasOverlappingElementBefore(pattern, i))
            {
                return RewriteWhitespaceQuantifier(pattern, i, quantifierEnd);
            }
        }

        return pattern;
    }

    private static string RewriteWhitespaceQuantifier(string pattern, int backslashIndex, int quantifierEnd)
    {
        var quantifierIndex = backslashIndex + GeneratedRegexHelpers.WhitespaceEscapeLength;
        return pattern[quantifierIndex] is '+'
            ? pattern.Substring(0, quantifierIndex) + pattern.Substring(quantifierEnd)
            : pattern.Substring(0, backslashIndex) + pattern.Substring(quantifierEnd);
    }
}
