using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace E128.Analyzers.Reliability;

internal static class GeneratedRegexHelpers
{
    private const int EscapeSequenceLength = 2;
    private const int NonCapturingPrefixLength = 3;
    private const int RangeEntryLength = 2;
    internal const int WhitespaceEscapeLength = 2;

    /// <summary>
    ///     The span of the unbounded outer quantifier of one repeated group with an inner
    ///     quantifier. <see cref="SingleAtomBody" /> is true when the body is one atom with one
    ///     quantifier, so removing the outer quantifier keeps the pattern language.
    /// </summary>
    internal readonly struct NestedQuantifierSpan : IEquatable<NestedQuantifierSpan>
    {
        private const int HashPrime = 31;

        internal NestedQuantifierSpan(int openIndex, int closeIndex, int quantifierIndex, int quantifierLength, bool singleAtomBody)
        {
            OpenIndex = openIndex;
            CloseIndex = closeIndex;
            QuantifierIndex = quantifierIndex;
            QuantifierLength = quantifierLength;
            SingleAtomBody = singleAtomBody;
        }

        internal int OpenIndex { get; }

        internal int CloseIndex { get; }

        internal int QuantifierIndex { get; }

        internal int QuantifierLength { get; }

        internal bool SingleAtomBody { get; }

        public bool Equals(NestedQuantifierSpan other)
        {
            return OpenIndex == other.OpenIndex
                   && CloseIndex == other.CloseIndex
                   && QuantifierIndex == other.QuantifierIndex
                   && QuantifierLength == other.QuantifierLength
                   && SingleAtomBody == other.SingleAtomBody;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is NestedQuantifierSpan other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = OpenIndex.GetHashCode();
                hash = (hash * HashPrime) + CloseIndex.GetHashCode();
                hash = (hash * HashPrime) + QuantifierIndex.GetHashCode();
                hash = (hash * HashPrime) + QuantifierLength.GetHashCode();
                return (hash * HashPrime) + (SingleAtomBody ? 1 : 0);
            }
        }

        public override string ToString()
        {
            var culture = CultureInfo.InvariantCulture;
            return string.Concat(
                "NestedQuantifierSpan(open: ",
                OpenIndex.ToString(culture),
                ", close: ",
                CloseIndex.ToString(culture),
                ", quantifier: ",
                QuantifierIndex.ToString(culture),
                ", length: ",
                QuantifierLength.ToString(culture),
                ", singleAtomBody: ",
                SingleAtomBody.ToString(culture),
                ")");
        }
    }

    internal static bool HasNestedQuantifier(string pattern)
    {
        return FindNestedQuantifierSpan(pattern) is not null;
    }

    internal static NestedQuantifierSpan? FindNestedQuantifierSpan(string pattern)
    {
        var inClass = BuildCharacterClassMask(pattern);
        var skipNext = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (inClass[i])
            {
                continue;
            }

            if (pattern[i] is not '(')
            {
                skipNext = pattern[i] is '\\';
                continue;
            }

            if (TryMatchNestedQuantifier(pattern, i, inClass) is { } span)
            {
                return span;
            }
        }

        return null;
    }

    private static NestedQuantifierSpan? TryMatchNestedQuantifier(string pattern, int openIndex, bool[] inClass)
    {
        if (IsAtomicGroup(pattern, openIndex))
        {
            return null;
        }

        var closeIndex = FindMatchingCloseParenForward(pattern, openIndex, inClass);
        if (closeIndex < 0)
        {
            return null;
        }

        var afterClose = closeIndex + 1;
        var quantifierLength = UnboundedQuantifierLength(pattern, afterClose);
        if (quantifierLength is 0 || !GroupContainsInnerQuantifier(pattern, openIndex, closeIndex, inClass))
        {
            return null;
        }

        if (!FirstAtomCanAlsoBeLast(pattern, openIndex, closeIndex, inClass))
        {
            return null;
        }

        return new NestedQuantifierSpan(openIndex, closeIndex, afterClose, quantifierLength, HasSingleAtomBody(pattern, openIndex, closeIndex));
    }

    internal static bool IsAtomicGroup(string pattern, int openIndex)
    {
        return openIndex + EscapeSequenceLength < pattern.Length
               && pattern[openIndex + 1] is '?'
               && pattern[openIndex + EscapeSequenceLength] is '>';
    }

    internal static int UnboundedQuantifierLength(string pattern, int index)
    {
        if (index >= pattern.Length)
        {
            return 0;
        }

        var length = pattern[index] is '*' or '+'
            ? 1
            : UnboundedBraceLength(pattern, index);
        if (length is 0)
        {
            return 0;
        }

        return index + length < pattern.Length && pattern[index + length] is '?' ? length + 1 : length;
    }

    private static int UnboundedBraceLength(string pattern, int index)
    {
        if (pattern[index] is not '{')
        {
            return 0;
        }

        var i = index + 1;
        var start = i;
        while (i < pattern.Length && IsAsciiDigit(pattern[i]))
        {
            i++;
        }

        if (i == start || i >= pattern.Length || pattern[i] is not ',')
        {
            return 0;
        }

        i++;
        if (i >= pattern.Length || pattern[i] is not '}')
        {
            return 0;
        }

        return i - index + 1;
    }

    private static bool HasSingleAtomBody(string pattern, int openIndex, int closeIndex)
    {
        var bodyStart = GroupBodyStart(pattern, openIndex);
        if (bodyStart < 0)
        {
            return false;
        }

        var atomEnd = AtomEnd(pattern, bodyStart, closeIndex);
        if (atomEnd < 0 || atomEnd >= closeIndex || pattern[atomEnd] is not ('*' or '+'))
        {
            return false;
        }

        atomEnd++;
        if (atomEnd < closeIndex && pattern[atomEnd] is '?')
        {
            atomEnd++;
        }

        return atomEnd == closeIndex;
    }

    internal static int GroupBodyStart(string pattern, int openIndex)
    {
        var bodyIndex = openIndex + 1;
        if (bodyIndex >= pattern.Length)
        {
            return -1;
        }

        if (pattern[bodyIndex] is not '?')
        {
            return bodyIndex;
        }

        if (bodyIndex + 1 >= pattern.Length)
        {
            return -1;
        }

        return pattern[bodyIndex + 1] is ':' ? openIndex + NonCapturingPrefixLength : -1;
    }

    private static int AtomEnd(string pattern, int start, int limit)
    {
        if (start >= limit)
        {
            return -1;
        }

        if (pattern[start] is '\\')
        {
            return start + EscapeSequenceLength <= limit ? start + EscapeSequenceLength : -1;
        }

        if (pattern[start] is '[')
        {
            var close = FindClassCloseIndex(pattern, start);
            return close >= 0 && close < limit ? close + 1 : -1;
        }

        return start + 1;
    }

    private static bool FirstAtomCanAlsoBeLast(string pattern, int openIndex, int closeIndex, bool[] inClass)
    {
        var bodyStart = GroupBodyStart(pattern, openIndex);
        if (bodyStart < 0 || HasTopLevelAlternation(pattern, bodyStart, closeIndex, inClass))
        {
            return true;
        }

        if (!TryReadFirstAtomSpan(pattern, bodyStart, closeIndex, out var firstStart, out var firstEnd)
            || !TryReadLastAtomSpan(pattern, bodyStart, closeIndex, inClass, out var lastStart, out var lastEnd))
        {
            return true;
        }

        return AtomsCanMatchSameText(pattern, firstStart, firstEnd, lastStart, lastEnd);
    }

    private static bool HasTopLevelAlternation(string pattern, int start, int limit, bool[] inClass)
    {
        var depth = 0;
        var skipNext = false;
        for (var j = start; j < limit; j++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (inClass[j])
            {
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
            else if (ch is '|' && depth is 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryReadFirstAtomSpan(
        string pattern,
        int bodyStart,
        int limit,
        out int start,
        out int end)
    {
        start = bodyStart;
        end = -1;
        if (bodyStart >= limit)
        {
            return false;
        }

        if (pattern[bodyStart] is '\\')
        {
            if (bodyStart + EscapeSequenceLength > limit)
            {
                return false;
            }

            end = bodyStart + EscapeSequenceLength;
            return true;
        }

        if (pattern[bodyStart] is '[')
        {
            var close = FindClassCloseIndex(pattern, bodyStart);
            if (close < 0 || close >= limit)
            {
                return false;
            }

            end = close + 1;
            return true;
        }

        if (pattern[bodyStart] is '(' or ')')
        {
            return false;
        }

        end = bodyStart + 1;
        return true;
    }

    private static bool TryReadLastAtomSpan(
        string pattern,
        int bodyStart,
        int closeIndex,
        bool[] inClass,
        out int start,
        out int end)
    {
        start = -1;
        end = -1;
        var atomEnd = LastAtomEndIndex(pattern, bodyStart, closeIndex, inClass);
        if (atomEnd < 0 || pattern[atomEnd] is ')')
        {
            return false;
        }

        if (pattern[atomEnd] is ']' && atomEnd > bodyStart)
        {
            return TryReadClassSpanEndingAt(pattern, bodyStart, atomEnd, out start, out end);
        }

        if (atomEnd > bodyStart && pattern[atomEnd - 1] is '\\' && !inClass[atomEnd - 1])
        {
            start = atomEnd - 1;
            end = atomEnd + 1;
            return true;
        }

        start = atomEnd;
        end = atomEnd + 1;
        return true;
    }

    private static int LastAtomEndIndex(string pattern, int bodyStart, int closeIndex, bool[] inClass)
    {
        var j = closeIndex - 1;
        while (j > bodyStart && !inClass[j] && IsTrailingQuantifierChar(pattern[j]))
        {
            j = pattern[j] is '}' ? SkipBoundedRepeatPrefix(pattern, bodyStart, j) : j - 1;
            if (j < 0)
            {
                return -1;
            }
        }

        return j;
    }

    private static bool IsTrailingQuantifierChar(char ch)
    {
        return ch is '?' or '*' or '+' or '}';
    }

    private static int SkipBoundedRepeatPrefix(string pattern, int bodyStart, int from)
    {
        var j = from;
        while (j > bodyStart && pattern[j] is not '{')
        {
            j--;
        }

        return pattern[j] is '{' ? j - 1 : -1;
    }

    private static bool TryReadClassSpanEndingAt(string pattern, int bodyStart, int classEnd, out int start, out int end)
    {
        start = -1;
        end = -1;
        var bracket = classEnd - 1;
        while (bracket > bodyStart && pattern[bracket] is not '[')
        {
            bracket--;
        }

        if (pattern[bracket] is not '[')
        {
            return false;
        }

        start = bracket;
        end = classEnd + 1;
        return true;
    }

    internal static bool AtomsCanMatchSameText(string pattern, int firstStart, int firstEnd, int lastStart, int lastEnd)
    {
        if (IsWildcardDot(pattern, firstStart, firstEnd) || IsWildcardDot(pattern, lastStart, lastEnd))
        {
            return true;
        }

        if (pattern[firstStart] is '[')
        {
            return ClassAtomOverlaps(pattern, firstStart, firstEnd, lastStart, lastEnd);
        }

        if (pattern[lastStart] is '[')
        {
            return ClassAtomOverlaps(pattern, lastStart, lastEnd, firstStart, firstEnd);
        }

        if (SpansEqual(pattern, firstStart, firstEnd, lastStart, lastEnd))
        {
            return true;
        }

        if (IsShorthandSpan(pattern, firstStart, firstEnd))
        {
            return OverlapsShorthand(pattern[firstStart + 1], pattern, lastStart, lastEnd);
        }

        return IsShorthandSpan(pattern, lastStart, lastEnd)
               && OverlapsShorthand(pattern[lastStart + 1], pattern, firstStart, firstEnd);
    }

    private static bool ClassAtomOverlaps(string pattern, int classStart, int classEnd, int otherStart, int otherEnd)
    {
        if (!TryReadLiteralChar(pattern, otherStart, otherEnd, out var c))
        {
            return true;
        }

        return ClassMatchesChar(pattern, classStart, classEnd - 1, c);
    }

    private static bool TryReadLiteralChar(string pattern, int start, int end, out char value)
    {
        if (end - start is 1)
        {
            value = pattern[start];
            return true;
        }

        if (end - start is EscapeSequenceLength && pattern[start] is '\\' && !IsClassShorthand(pattern[start + 1]))
        {
            return TryEscapedChar(pattern[start + 1], out value);
        }

        value = '\0';
        return false;
    }

    private static bool IsClassShorthand(char c)
    {
        return c is 's' or 'S' or 'd' or 'D' or 'w' or 'W';
    }

    private static bool ClassMatchesChar(string pattern, int start, int close, char c)
    {
        var index = start + 1;
        var negated = false;
        if (pattern[index] is '^')
        {
            negated = true;
            index++;
        }

        if (pattern[index] is ']')
        {
            index++;
        }

        var anyMatch = false;
        while (index < close && !anyMatch)
        {
            var end = ReadClassEntry(pattern, index, close, c, ref anyMatch);
            if (end < 0)
            {
                return true;
            }

            index = end;
        }

        return negated ? !anyMatch : anyMatch;
    }

    private static int ReadClassEntry(string pattern, int index, int close, char c, ref bool anyMatch)
    {
        var next = index + 1;
        if (pattern[index] is '\\')
        {
            return ReadEscapedClassEntry(pattern, next, close, c, ref anyMatch);
        }

        return ReadLiteralClassEntry(pattern, pattern[index], next, close, c, ref anyMatch);
    }

    private static int ReadEscapedClassEntry(string pattern, int next, int close, char c, ref bool anyMatch)
    {
        if (next >= close)
        {
            return -1;
        }

        var escaped = pattern[next];
        if (IsClassShorthand(escaped))
        {
            anyMatch = ShorthandIncludesChar(escaped, c);
            return next + 1;
        }

        if (!TryEscapedChar(escaped, out var lo))
        {
            return -1;
        }

        return ReadLiteralClassEntry(pattern, lo, next + 1, close, c, ref anyMatch);
    }

    private static int ReadLiteralClassEntry(string pattern, char lo, int next, int close, char c, ref bool anyMatch)
    {
        if (IsRangeStart(pattern, next, close))
        {
            var hi = pattern[next + 1];
            if (lo > hi)
            {
                return -1;
            }

            anyMatch = c >= lo && c <= hi;
            return next + RangeEntryLength;
        }

        anyMatch = c == lo;
        return next;
    }

    private static bool IsRangeStart(string pattern, int index, int close)
    {
        return index + 1 < close
               && pattern[index] is '-'
               && pattern[index + 1] is not ']' and not '\\';
    }

    private static bool TryEscapedChar(char escaped, out char value)
    {
        if (IsSimpleCharEscape(escaped))
        {
            value = ControlEscapeChar(escaped);
            return true;
        }

        if (IsAsciiLetter(escaped) || IsAsciiDigit(escaped))
        {
            value = '\0';
            return false;
        }

        value = escaped;
        return true;
    }

    private static char ControlEscapeChar(char escaped)
    {
        return escaped switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'f' => '\f',
            'v' => '\v',
            'b' => '\b',
            '0' => '\0',
            _ => escaped
        };
    }

    private static bool IsSimpleCharEscape(char escaped)
    {
        return escaped is 'n' or 'r' or 't' or 'f' or 'v' or 'b' or '0';
    }

    private static bool IsShorthandSpan(string pattern, int start, int end)
    {
        return end - start == EscapeSequenceLength && pattern[start] is '\\' && IsAsciiLetter(pattern[start + 1]);
    }

    private static bool OverlapsShorthand(char kind, string pattern, int otherStart, int otherEnd)
    {
        if (otherEnd - otherStart is not 1)
        {
            return !IsClassBoundary(pattern, otherStart);
        }

        return ShorthandIncludesChar(kind, pattern[otherStart]);
    }

    private static bool ShorthandIncludesChar(char kind, char c)
    {
        return kind switch
        {
            'd' => IsAsciiDigit(c),
            'D' => !IsAsciiDigit(c),
            'w' => IsWordChar(c),
            'W' => !IsWordChar(c),
            's' => char.IsWhiteSpace(c),
            'S' => !char.IsWhiteSpace(c),
            _ => true
        };
    }

    private static bool IsClassBoundary(string pattern, int start)
    {
        return pattern[start] is '[' or ']';
    }

    private static bool IsWildcardDot(string pattern, int start, int end)
    {
        return end - start is 1 && pattern[start] is '.';
    }

    internal static bool IsAsciiDigit(char c)
    {
        return c is >= '0' and <= '9';
    }

    private static bool IsAsciiLetter(char c)
    {
        return c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
    }

    private static bool IsWordChar(char c)
    {
        return IsAsciiLetter(c) || IsAsciiDigit(c) || c is '_';
    }

    private static bool SpansEqual(string pattern, int firstStart, int firstEnd, int lastStart, int lastEnd)
    {
        if (firstEnd - firstStart != lastEnd - lastStart)
        {
            return false;
        }

        for (var i = 0; i < firstEnd - firstStart; i++)
        {
            if (pattern[firstStart + i] != pattern[lastStart + i])
            {
                return false;
            }
        }

        return true;
    }


    internal static bool GroupContainsInnerQuantifier(string pattern, int openIndex, int closeIndex, bool[] inClass)
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

            if (inClass[j])
            {
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

    internal static int FindMatchingCloseParenForward(string pattern, int openIndex, bool[] inClass)
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

            if (inClass[j])
            {
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

    internal static bool HasOverlappingQuantifiers(string pattern)
    {
        var inClass = BuildCharacterClassMask(pattern);
        for (var i = 0; i < pattern.Length - WhitespaceEscapeLength; i++)
        {
            if (inClass[i])
            {
                continue;
            }

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

    internal static bool[] BuildCharacterClassMask(string pattern)
    {
        var inClass = new bool[pattern.Length];
        var index = 0;
        while (index < pattern.Length)
        {
            if (pattern[index] is '\\')
            {
                index += EscapeSequenceLength;
                continue;
            }

            if (pattern[index] is not '[')
            {
                index++;
                continue;
            }

            var closeIndex = FindClassCloseIndex(pattern, index);
            if (closeIndex < 0)
            {
                for (var k = index; k < pattern.Length; k++)
                {
                    inClass[k] = true;
                }

                break;
            }

            for (var k = index; k < closeIndex; k++)
            {
                inClass[k] = true;
            }

            index = closeIndex + 1;
        }

        return inClass;
    }

    internal static int FindClassCloseIndex(string pattern, int bracketIndex)
    {
        var i = bracketIndex + 1;
        if (i < pattern.Length && pattern[i] is '^')
        {
            i++;
        }

        if (i < pattern.Length && pattern[i] is ']')
        {
            i++;
        }

        while (i < pattern.Length)
        {
            if (pattern[i] is '\\')
            {
                i += EscapeSequenceLength;
                continue;
            }

            if (pattern[i] is ']')
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    internal static int WhitespaceQuantifierEnd(string pattern, int index)
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

    internal static bool HasOverlappingElementAfter(string pattern, int index)
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

    internal static bool HasOverlappingElementBefore(string pattern, int backslashSIndex)
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
        var inClass = BuildCharacterClassMask(pattern);
        var depth = 0;
        var skipNext = false;
        for (var j = openParenIndex; j < pattern.Length; j++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (inClass[j])
            {
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
        var inClass = BuildCharacterClassMask(pattern);
        for (var j = closeIndex; j >= 0; j--)
        {
            if (j > 0 && pattern[j - 1] is '\\')
            {
                continue;
            }

            if (inClass[j])
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
