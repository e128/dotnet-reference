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
    // The length of the escaped whitespace class: a backslash and the letter s.
    private const int EscapedClassLength = 2;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GeneratedRegexAnalyzer.OverlappingQuantifierDiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not AttributeSyntax)
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Remove overlapping \\s quantifier", nameof(GeneratedRegexOverlappingCodeFixProvider), ct => RegexLiteralFix.ApplyRewriteAsync(context.Document, node, RemoveOverlappingWhitespaceQuantifier, ct));

        return Task.CompletedTask;
    }

    internal static string RemoveOverlappingWhitespaceQuantifier(string pattern)
    {
        for (var i = 0; i < pattern.Length - EscapedClassLength; i++)
        {
            var qEnd = FindWhitespaceQuantifierEnd(pattern, i);
            if (qEnd < 0)
            {
                continue;
            }

            if (HasOverlappingElementForward(pattern, qEnd) || HasOverlappingElementBackward(pattern, i))
            {
                return pattern.Substring(0, i) + pattern.Substring(qEnd);
            }
        }

        return pattern;
    }

    // Returns the index after a `\s*` or `\s+` quantifier (with an optional lazy `?`) that starts at
    // the index, or -1 when no such quantifier starts there.
    private static int FindWhitespaceQuantifierEnd(string pattern, int index)
    {
        if (pattern[index] is not '\\' || pattern[index + 1] is not 's')
        {
            return -1;
        }

        var qEnd = index + EscapedClassLength;
        if (qEnd >= pattern.Length || pattern[qEnd] is not ('*' or '+'))
        {
            return -1;
        }

        qEnd++;
        if (qEnd < pattern.Length && pattern[qEnd] is '?')
        {
            qEnd++;
        }

        return qEnd;
    }

    private static bool HasOverlappingElementForward(string pattern, int index)
    {
        if (index >= pattern.Length)
        {
            return false;
        }

        var ch = pattern[index];

        return (ch is '.' && index + 1 < pattern.Length && pattern[index + 1] is '*' or '+')
               || (ch is '(' && ContainsDotQuantifierInGroup(pattern, index));
    }

    private static bool HasOverlappingElementBackward(string pattern, int backslashIndex)
    {
        if (backslashIndex is 0)
        {
            return false;
        }

        var prevIdx = backslashIndex - 1;
        if (pattern[prevIdx] is '?')
        {
            if (prevIdx is 0)
            {
                return false;
            }

            prevIdx--;
        }

        var prev = pattern[prevIdx];

        if (IsQuantifierPrecededBy(pattern, prev, prevIdx, '.'))
        {
            return true;
        }

        if (prev is ')')
        {
            return IsDotQuantifierGroupClosedAt(pattern, prevIdx);
        }

        return IsQuantifierPrecededBy(pattern, prev, prevIdx, ')')
            && IsDotQuantifierGroupClosedAt(pattern, prevIdx - 1);
    }

    private static bool IsQuantifierPrecededBy(string pattern, char prev, int prevIdx, char expected)
    {
        return prev is '*' or '+' && prevIdx > 0 && pattern[prevIdx - 1] == expected;
    }

    private static bool IsDotQuantifierGroupClosedAt(string pattern, int closeIndex)
    {
        var openParen = FindMatchingOpenParen(pattern, closeIndex);
        return openParen >= 0 && ContainsDotQuantifierInGroup(pattern, openParen);
    }

    private static bool ContainsDotQuantifierInGroup(string pattern, int openParenIndex)
    {
        if (openParenIndex < 0)
        {
            return false;
        }

        var skipNext = false;
        var depth = 0;
        for (var j = openParenIndex; j < pattern.Length; j++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            var ch = pattern[j];
            if (ch is '\\')
            {
                skipNext = true;
                continue;
            }

            depth += GroupDepthDelta(ch);
            if (ch is ')' && depth is 0)
            {
                return false;
            }

            if (IsDotQuantifierAt(pattern, j, depth))
            {
                return true;
            }
        }

        return false;
    }

    private static int GroupDepthDelta(char ch)
    {
        return ch switch
        {
            '(' => 1,
            ')' => -1,
            _ => 0
        };
    }

    private static bool IsDotQuantifierAt(string pattern, int index, int depth)
    {
        return pattern[index] is '.'
               && depth is 1
               && index + 1 < pattern.Length
               && pattern[index + 1] is '*' or '+' or '?';
    }

    private static int FindMatchingOpenParen(string pattern, int closeIndex)
    {
        var depth = 0;
        for (var j = closeIndex; j >= 0; j--)
        {
            if (j > 0 && pattern[j - 1] is '\\')
            {
                continue;
            }

            if (pattern[j] is ')')
            {
                depth++;
            }
            else if (pattern[j] is '(')
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
