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
            else if (ch is '*' or '+' && depth is 1)
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
                if (depth is 0)
                {
                    return j;
                }
            }
        }

        return -1;
    }

    private const int WhitespaceEscapeLength = 2;

    internal static bool HasOverlappingQuantifiers(string pattern)
    {
        for (var i = 0; i < pattern.Length - WhitespaceEscapeLength; i++)
        {
            var afterIndex = WhitespaceQuantifierEnd(pattern, i);
            if (afterIndex < 0)
            {
                continue;
            }

            if (HasOverlappingElementAfter(pattern, afterIndex) || HasOverlappingElementBefore(pattern, i))
            {
                return true;
            }
        }

        return false;
    }

    // Returns the index after a whitespace escape and its quantifier, or -1 when none starts at index.
    private static int WhitespaceQuantifierEnd(string pattern, int index)
    {
        if (pattern[index] != '\\' || pattern[index + 1] != 's')
        {
            return -1;
        }

        var quantifierIndex = index + WhitespaceEscapeLength;
        if (quantifierIndex >= pattern.Length || pattern[quantifierIndex] is not '*' and not '+')
        {
            return -1;
        }

        var afterIndex = quantifierIndex + 1;
        return afterIndex < pattern.Length && pattern[afterIndex] is '?' ? afterIndex + 1 : afterIndex;
    }

    private static bool HasOverlappingElementAfter(string pattern, int index)
    {
        if (index >= pattern.Length)
        {
            return false;
        }

        var ch = pattern[index];

        if (ch is '.')
        {
            return IsQuantifierAfter(pattern, index);
        }

        if (ch is '(')
        {
            return GroupContainsOverlappingContent(pattern, index);
        }

        return ch is '[' && index + 1 < pattern.Length && pattern[index + 1] is '^' && NegatedClassOverlapsWhitespace(pattern, index);
    }

    private static bool IsQuantifierAfter(string pattern, int index)
    {
        return index + 1 < pattern.Length && pattern[index + 1] is '*' or '+' or '?';
    }

    private static bool HasOverlappingElementBefore(string pattern, int backslashSIndex)
    {
        if (backslashSIndex is 0)
        {
            return false;
        }

        var prevIndex = backslashSIndex - 1;
        var prevCh = pattern[prevIndex];

        if (prevCh is '?')
        {
            if (prevIndex is 0)
            {
                return false;
            }

            prevIndex--;
            prevCh = pattern[prevIndex];
        }

        if (prevCh is '*' or '+')
        {
            return QuantifiedElementOverlaps(pattern, prevIndex);
        }

        return prevCh is ')' && GroupBeforeOverlaps(pattern, prevIndex);
    }

    private static bool QuantifiedElementOverlaps(string pattern, int quantifierIndex)
    {
        if (quantifierIndex is 0)
        {
            return false;
        }

        var elementCh = pattern[quantifierIndex - 1];
        if (elementCh is '.')
        {
            return true;
        }

        return elementCh is ')' && GroupBeforeOverlaps(pattern, quantifierIndex - 1);
    }

    private static bool GroupBeforeOverlaps(string pattern, int closeIndex)
    {
        var openParen = FindMatchingOpenParen(pattern, closeIndex);
        return openParen >= 0 && GroupContainsOverlappingContent(pattern, openParen);
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

            var verdict = ClassifyGroupChar(pattern, j, ref depth);
            if (verdict is not null)
            {
                return verdict.Value;
            }
        }

        return false;
    }

    // Returns a final answer when the scan can stop, or null to keep scanning.
    private static bool? ClassifyGroupChar(string pattern, int index, ref int depth)
    {
        var ch = pattern[index];
        if (ch is '(')
        {
            depth++;
            return null;
        }

        if (ch is ')')
        {
            depth--;
            return depth is 0 ? false : null;
        }

        if (ch is '.' && depth is 1 && IsQuantifierAfter(pattern, index))
        {
            return true;
        }

        return null;
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
                if (depth is 0)
                {
                    return j;
                }
            }
        }

        return -1;
    }
}
