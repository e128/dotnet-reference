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
            || string.Equals(RemoveOuterQuantifier(pattern), pattern, StringComparison.Ordinal))
        {
            // This shape has no language-preserving one-token rewrite, so no action is offered.
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Remove outer quantifier from nested group", nameof(GeneratedRegexNestedCodeFixProvider), ct => RegexLiteralFix.ApplyRewriteAsync(context.Document, node, RemoveOuterQuantifier, ct));

        return Task.CompletedTask;
    }

    // A single-atom body makes the outer quantifier redundant, so the rewrite removes it
    // and the diagnostic clears. Any other body keeps the language with an equivalent
    // unbounded brace repeat. An unbounded brace outer quantifier has no
    // language-preserving one-token rewrite and stays flagged.
    internal static string RemoveOuterQuantifier(string pattern)
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
            return pattern.Remove(quantifierIndex, span.QuantifierLength);
        }

        var lazy = span.QuantifierLength > 1 && pattern[quantifierIndex + 1] is '?';
        var replacement = string.Concat(pattern[quantifierIndex] is '*' ? "{0,}" : "{1,}", lazy ? "?" : string.Empty);
        return pattern.Substring(0, quantifierIndex) + replacement + pattern.Substring(quantifierIndex + span.QuantifierLength);
    }
}
