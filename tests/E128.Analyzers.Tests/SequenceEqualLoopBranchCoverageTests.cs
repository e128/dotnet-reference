using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SequenceEqualLoopBranchCoverageTests
{
    private const string Enums = """
        enum Color : byte
        {
            Red,
            Green,
        }

        """;

    private static Task VerifyAsync(string parameters, string body)
    {
        var code = Enums
                   + "class C\n{\n    bool M(" + parameters + ")\n    {\n"
                   + body
                   + "    }\n}\n";
        return AnalyzerTestRunner.VerifyAsync<SequenceEqualLoopAnalyzer>(code);
    }

    private static string Loop(string markup, string loopHeader = "for (int i = 0; i < left.Length; i++)", string comparison = "left[i] != right[i]")
    {
        return $$"""
                    if (left.Length != right.Length)
                    {
                        return false;
                    }

                    {{markup}}{{loopHeader}}
                    {
                        if ({{comparison}})
                        {
                            return false;
                        }
                    }{{(markup.Length > 0 ? "|}" : string.Empty)}}
                    return true;

            """;
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("bool")]
    [InlineData("char")]
    [InlineData("sbyte")]
    [InlineData("byte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    [InlineData("uint")]
    [InlineData("long")]
    [InlineData("ulong")]
    [InlineData("Color")]
    public Task Reports_ForIntegralAndEnumElements(string element)
    {
        return VerifyAsync(element + "[] left, " + element + "[] right", Loop("{|E128121:"));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("string")]
    [InlineData("object")]
    [InlineData("decimal")]
    [InlineData("float")]
    [InlineData("double")]
    public Task Silent_ForOtherElementTypes(string element)
    {
        return VerifyAsync(element + "[] left, " + element + "[] right", Loop(string.Empty));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLoopBoundIsTheRightArray()
    {
        return VerifyAsync(
            "int[] left, int[] right",
            Loop("{|E128121:", "for (int i = 0; i < right.Length; i++)"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardComparesTheLengthsInTheOtherOrder()
    {
        return VerifyAsync(
            "int[] left, int[] right",
            """
                    if (right.Length != left.Length) return false;

                    {|E128121:for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return false;
                        }
                    }|}
                    return true;

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardComparesOtherArrays()
    {
        return VerifyAsync(
            "int[] left, int[] right, int[] other",
            """
                    if (other.Length != right.Length)
                    {
                        return false;
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return false;
                        }
                    }
                    return true;

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardDoesNotReturnFalse()
    {
        return VerifyAsync(
            "int[] left, int[] right",
            """
                    if (left.Length != right.Length)
                    {
                        return true;
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return false;
                        }
                    }
                    return true;

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardBodyDoesMoreThanReturn()
    {
        return VerifyAsync(
            "int[] left, int[] right",
            """
                    if (left.Length != right.Length)
                    {
                        left = right;
                        return false;
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return false;
                        }
                    }
                    return true;

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardHasAnElseBranch()
    {
        return VerifyAsync(
            "int[] left, int[] right",
            """
                    if (left.Length != right.Length)
                    {
                        return false;
                    }
                    else
                    {
                        left = right;
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return false;
                        }
                    }
                    return true;

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardComparesSomethingOtherThanLengths()
    {
        return VerifyAsync(
            "int[] left, int[] right",
            """
                    if (left.Length != 3)
                    {
                        return false;
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return false;
                        }
                    }
                    return true;

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNothingFollowsTheLoopOrItDoesNotReturnTrue()
    {
        return VerifyAsync(
            "int[] left, int[] right",
            """
                    if (left.Length != right.Length)
                    {
                        return false;
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return false;
                        }
                    }

                    return false;

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopIsTheLastStatement()
    {
        return AnalyzerTestRunner.VerifyAsync<SequenceEqualLoopAnalyzer>(
            """
            class C
            {
                void M(int[] left, int[] right)
                {
                    if (left.Length != right.Length)
                    {
                        return;
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return;
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheComparisonIsNotNotEquals()
    {
        return VerifyAsync(
            "int[] left, int[] right",
            Loop(string.Empty, comparison: "left[i] == right[i]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnOperandIsNotAnElementOfTheLoopIndex()
    {
        return VerifyAsync(
            "int[] left, int[] right, int[,] grid",
            Loop(string.Empty, comparison: "left[i] != 0")
            + Loop(string.Empty, comparison: "left[i + 1] != right[i]")
            + Loop(string.Empty, comparison: "grid[i, 0] != right[i]")
            + Loop(string.Empty, comparison: "left[0] != right[i]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArraysHaveDifferentElementTypes()
    {
        return VerifyAsync("int[] left, long[] right", Loop(string.Empty));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheOperandsAreStringsOrMultiDimensional()
    {
        return VerifyAsync(
            "string left, string right",
            Loop(string.Empty));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopBodyIsNotASingleIfReturn()
    {
        return VerifyAsync(
            "int[] left, int[] right",
            """
                    if (left.Length != right.Length)
                    {
                        return false;
                    }

                    for (int i = 0; i < left.Length; i++)
                        if (left[i] != right[i])
                        {
                            return false;
                        }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i]) return false;
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return true;
                        }
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return false;
                        }

                        continue;
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        _ = left[i];
                    }

                    return true;

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopIsNotDirectlyInABlock()
    {
        return VerifyAsync(
            "int[] left, int[] right, bool flag",
            """
                    if (flag)
                        for (int i = 0; i < left.Length; i++)
                        {
                            if (left[i] != right[i])
                            {
                                return false;
                            }
                        }

                    return true;

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardIsAnUnbracedReturn()
    {
        return VerifyAsync(
            "int[] left, int[] right",
            """
                    if (left.Length != right.Length) return false;
                    {|E128121:for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return false;
                        }
                    }|}
                    return true;

            """);
    }
}
