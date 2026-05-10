namespace E128.Analyzers.Reliability;

internal static class GeneratedRegexHelpers
{
    internal static bool HasNestedQuantifier(string pattern)
    {
        var skipNext = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (pattern[i] is '\\')
            {
                skipNext = true;
                continue;
            }

            if (pattern[i] is not '(')
            {
                continue;
            }

            var closeIndex = FindMatchingCloseParenForward(pattern, i);
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
                return true;
            }
        }

        return false;
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
            if (ch is '\\')
            {
                skipNext = true;
                continue;
            }

            if (ch is '(')
            {
                depth++;
            }
            else if (ch is ')')
            {
                depth--;
            }
            else if (ch is '*' or '+' && depth == 1)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindMatchingCloseParenForward(string pattern, int openIndex)
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

            if (pattern[j] is '\\')
            {
                skipNext = true;
                continue;
            }

            if (pattern[j] is '(')
            {
                depth++;
            }
            else if (pattern[j] is ')')
            {
                depth--;
                if (depth == 0)
                {
                    return j;
                }
            }
        }

        return -1;
    }

    internal static bool HasOverlappingQuantifiers(string pattern)
    {
        for (var i = 0; i < pattern.Length - 2; i++)
        {
            if (pattern[i] != '\\' || pattern[i + 1] != 's')
            {
                continue;
            }

            var quantifierIndex = i + 2;
            if (quantifierIndex >= pattern.Length)
            {
                continue;
            }

            if (pattern[quantifierIndex] is not '*' and not '+')
            {
                continue;
            }

            var afterIndex = quantifierIndex + 1;
            if (afterIndex < pattern.Length && pattern[afterIndex] is '?')
            {
                afterIndex++;
            }

            if (HasOverlappingElementAfter(pattern, afterIndex))
            {
                return true;
            }

            if (HasOverlappingElementBefore(pattern, i))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOverlappingElementAfter(string pattern, int index)
    {
        if (index >= pattern.Length)
        {
            return false;
        }

        var ch = pattern[index];

        return ch is '.'
            ? index + 1 < pattern.Length && pattern[index + 1] is '*' or '+' or '?'
            : ch is '('
                ? GroupContainsOverlappingContent(pattern, index)
                : ch is '[' && index + 1 < pattern.Length && pattern[index + 1] is '^' && NegatedClassOverlapsWhitespace(pattern, index);
    }

    private static bool HasOverlappingElementBefore(string pattern, int backslashSIndex)
    {
        if (backslashSIndex == 0)
        {
            return false;
        }

        var prevIndex = backslashSIndex - 1;
        var prevCh = pattern[prevIndex];

        if (prevCh is '?')
        {
            if (prevIndex == 0)
            {
                return false;
            }

            prevIndex--;
            prevCh = pattern[prevIndex];
        }

        if (prevCh is '*' or '+')
        {
            if (prevIndex == 0)
            {
                return false;
            }

            var elementCh = pattern[prevIndex - 1];
            if (elementCh is '.')
            {
                return true;
            }

            if (elementCh is ')')
            {
                var openParen = FindMatchingOpenParen(pattern, prevIndex - 1);
                if (openParen >= 0)
                {
                    return GroupContainsOverlappingContent(pattern, openParen);
                }
            }
        }

        if (prevCh is ')')
        {
            var openParen = FindMatchingOpenParen(pattern, prevIndex);
            if (openParen >= 0)
            {
                return GroupContainsOverlappingContent(pattern, openParen);
            }
        }

        return false;
    }

    private static bool GroupContainsOverlappingContent(string pattern, int openParenIndex)
    {
        var depth = 0;
        var skipNext = false;
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

            if (ch is '(')
            {
                depth++;
            }
            else if (ch is ')')
            {
                depth--;
                if (depth == 0)
                {
                    return false;
                }
            }
            else if (ch is '.' && depth == 1)
            {
                var nextJ = j + 1;
                if (nextJ < pattern.Length && pattern[nextJ] is '*' or '+' or '?')
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool NegatedClassOverlapsWhitespace(string pattern, int bracketIndex)
    {
        var skipNext = false;
        for (var j = bracketIndex + 2; j < pattern.Length; j++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (pattern[j] is ']')
            {
                var afterClass = j + 1;
                return afterClass < pattern.Length && pattern[afterClass] is '*' or '+';
            }

            if (pattern[j] is '\\' && j + 1 < pattern.Length)
            {
                if (pattern[j + 1] is 's')
                {
                    return false;
                }

                skipNext = true;
            }
        }

        return false;
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
                if (depth == 0)
                {
                    return j;
                }
            }
        }

        return -1;
    }
}
