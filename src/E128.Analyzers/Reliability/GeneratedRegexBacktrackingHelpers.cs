using System.Collections.Generic;
using System.Globalization;
using static E128.Analyzers.Reliability.GeneratedRegexHelpers;

namespace E128.Analyzers.Reliability;

internal static class GeneratedRegexBacktrackingHelpers
{
    private const int ThreeWildcards = 3;

    internal static bool HasThreeUnboundedDots(string pattern)
    {
        var inClass = BuildCharacterClassMask(pattern);
        var count = 0;

        for (var i = 0; i < pattern.Length - 1; i++)
        {
            if (inClass[i] || pattern[i] is not '.' || (i > 0 && pattern[i - 1] is '\\'))
            {
                continue;
            }

            if (UnboundedQuantifierLength(pattern, i + 1) is 0)
            {
                continue;
            }

            count++;
            if (count >= ThreeWildcards)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasOptionalLastAtomInRepeatedGroup(string pattern)
    {
        var inClass = BuildCharacterClassMask(pattern);

        for (var i = 0; i < pattern.Length; i++)
        {
            if (inClass[i] || pattern[i] is not '(' || IsAtomicGroup(pattern, i))
            {
                continue;
            }

            var closeIndex = FindMatchingCloseParenForward(pattern, i, inClass);
            if (closeIndex < 0
                || UnboundedQuantifierLength(pattern, closeIndex + 1) is 0
                || !GroupContainsInnerQuantifier(pattern, i, closeIndex, inClass))
            {
                continue;
            }

            if (LastAtomIsOptional(pattern, i, closeIndex))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasOverlappingAlternation(string pattern)
    {
        var inClass = BuildCharacterClassMask(pattern);

        for (var i = 0; i < pattern.Length; i++)
        {
            if (inClass[i] || pattern[i] is not '(' || IsAtomicGroup(pattern, i))
            {
                continue;
            }

            var closeIndex = FindMatchingCloseParenForward(pattern, i, inClass);
            if (closeIndex < 0 || UnboundedQuantifierLength(pattern, closeIndex + 1) is 0)
            {
                continue;
            }

            if (BranchesOverlap(pattern, i, closeIndex, inClass))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BranchesOverlap(
        string pattern,
        int openIndex,
        int closeIndex,
        bool[] inClass)
    {
        var branches = ReadBranches(pattern, openIndex, closeIndex, inClass);

        return branches.Count > 1 && AnyPairOverlaps(pattern, branches);
    }

    private static bool LastAtomIsOptional(string pattern, int openIndex, int closeIndex)
    {
        var index = closeIndex - 1;
        if (index <= openIndex)
        {
            return false;
        }

        if (pattern[index] is '?')
        {
            var before = index - 1;
            return before > openIndex && pattern[before] is not ('+' or '*' or '}' or ')');
        }

        return pattern[index] is '*'
               || (pattern[index] is '}' && IsZeroLowerBoundBrace(pattern, openIndex, index));
    }

    private static bool IsZeroLowerBoundBrace(string pattern, int openIndex, int closeBrace)
    {
        var index = closeBrace - 1;
        while (index > openIndex && pattern[index] is not '{')
        {
            index--;
        }

        if (pattern[index] is not '{')
        {
            return false;
        }

        var start = index + 1;
        var digits = 0;
        while (start + digits < closeBrace && IsAsciiDigit(pattern[start + digits]))
        {
            digits++;
        }

        return digits > 0
               && int.TryParse(pattern.Substring(start, digits), NumberStyles.None, CultureInfo.InvariantCulture, out var lower)
               && lower is 0;
    }

    private static List<(int Start, int End)> ReadBranches(
        string pattern,
        int openIndex,
        int closeIndex,
        bool[] inClass)
    {
        var branches = new List<(int Start, int End)>();
        var bodyStart = GroupBodyStart(pattern, openIndex);

        if (bodyStart < 0)
        {
            return branches;
        }

        var depth = 0;
        var skipNext = false;
        var branchStart = bodyStart;

        for (var j = bodyStart; j <= closeIndex; j++)
        {
            if (!IsSeparatorAt(pattern, j, closeIndex, inClass, ref depth, ref skipNext))
            {
                continue;
            }

            branches.Add((branchStart, j));
            branchStart = j + 1;
        }

        branches.Add((branchStart, closeIndex));
        return branches;
    }

    private static bool IsSeparatorAt(
        string pattern,
        int index,
        int closeIndex,
        bool[] inClass,
        ref int depth,
        ref bool skipNext)
    {
        if (skipNext)
        {
            skipNext = false;
            return false;
        }

        if (inClass[index])
        {
            return false;
        }

        var ch = pattern[index];
        if (ch is '\\')
        {
            skipNext = true;
            return false;
        }

        if (ch is '(')
        {
            depth++;
            return false;
        }

        if (ch is ')' && index < closeIndex)
        {
            depth--;
            return false;
        }

        return ch is '|' && depth is 0;
    }

    private static bool AnyPairOverlaps(string pattern, List<(int Start, int End)> branches)
    {
        for (var first = 0; first < branches.Count; first++)
        {
            for (var second = first + 1; second < branches.Count; second++)
            {
                if (FirstAtomsOverlap(pattern, branches[first], branches[second]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool FirstAtomsOverlap(
        string pattern,
        (int Start, int End) first,
        (int Start, int End) second)
    {
        return TryReadFirstAtomSpan(pattern, first.Start, first.End, out var firstStart, out var firstEnd)
               && TryReadFirstAtomSpan(pattern, second.Start, second.End, out var secondStart, out var secondEnd)
               && AtomsCanMatchSameText(pattern, firstStart, firstEnd, secondStart, secondEnd);
    }
}
