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
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Fix the nested quantifier group", nameof(GeneratedRegexNestedCodeFixProvider), ct => RegexLiteralFix.ApplyRewriteAsync(context.Document, node, RewriteOuterQuantifier, ct));

        return Task.CompletedTask;
    }

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

        if (!span.SingleAtomBody)
        {
            return pattern;
        }

        if (RemovalKeepsLanguage(pattern, span))
        {
            return pattern.Remove(quantifierIndex, span.QuantifierLength);
        }

        return MakeGroupNonCapturing(pattern, span);
    }

    private static bool RemovalKeepsLanguage(string pattern, GeneratedRegexHelpers.NestedQuantifierSpan span)
    {
        if (pattern[span.QuantifierIndex] is not '*')
        {
            return true;
        }

        var innerQuantifierIndex = span.CloseIndex - 1;
        if (pattern[innerQuantifierIndex] is '?')
        {
            innerQuantifierIndex--;
        }

        return pattern[innerQuantifierIndex] is not '+';
    }

    private const int MarkerLength = 2;

    private static string MakeGroupNonCapturing(string pattern, GeneratedRegexHelpers.NestedQuantifierSpan span)
    {
        if (pattern[span.OpenIndex + 1] is '?' && pattern[span.OpenIndex + MarkerLength] is ':')
        {
            return pattern;
        }

        return pattern.Substring(0, span.OpenIndex + 1) + "?:" + pattern.Substring(span.OpenIndex + 1);
    }
}
