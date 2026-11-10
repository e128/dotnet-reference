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

        if (node is not AttributeSyntax)
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Remove outer quantifier from nested group", nameof(GeneratedRegexNestedCodeFixProvider), ct => RegexLiteralFix.ApplyRewriteAsync(context.Document, node, RemoveOuterQuantifier, ct));

        return Task.CompletedTask;
    }

    internal static string RemoveOuterQuantifier(string pattern)
    {
        var skipNext = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (pattern[i] == '\\')
            {
                skipNext = true;
                continue;
            }

            if (pattern[i] != '(')
            {
                continue;
            }

            var closeIndex = FindMatchingCloseParen(pattern, i);
            if (closeIndex < 0)
            {
                continue;
            }

            var afterClose = closeIndex + 1;
            if (afterClose >= pattern.Length || pattern[afterClose] is not ('*' or '+'))
            {
                continue;
            }

            if (GroupContainsInnerQuantifier(pattern, i, closeIndex))
            {
                return pattern.Substring(0, afterClose) + pattern.Substring(afterClose + 1);
            }
        }

        return pattern;
    }

    private static bool GroupContainsInnerQuantifier(string pattern, int openIndex, int closeIndex)
    {
        var skipNext = false;
        var depth = 0;
        for (var j = openIndex; j <= closeIndex; j++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            var ch = pattern[j];
            if (ch == '\\')
            {
                skipNext = true;
                continue;
            }

            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth--;
            }
            else if (ch is '*' or '+' && depth is 1)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindMatchingCloseParen(string pattern, int openIndex)
    {
        var depth = 0;
        var skipNext = false;
        for (var j = openIndex; j < pattern.Length; j++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (pattern[j] == '\\')
            {
                skipNext = true;
                continue;
            }

            if (pattern[j] == '(')
            {
                depth++;
            }
            else if (pattern[j] == ')')
            {
                depth--;
                if (depth is 0)
                {
                    return j;
                }
            }
        }

        return -1;
    }
}
