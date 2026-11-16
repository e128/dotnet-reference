using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ManualBitCountBranchCoverageTests
{
    private static Task VerifyAsync(
        string valueType,
        string countType,
        string condition,
        string first,
        string second,
        bool reports)
    {
        var loop = $$"""
                    while ({{condition}})
                    {
                        {{first}}
                        {{second}}
                    }
            """;
        var marked = reports ? "{|E128134:" + loop.TrimStart() + "|}" : loop.TrimStart();
        var code = $$"""
            class C
            {
                int Count({{valueType}} value, {{valueType}} other, int shift)
                {
                    {{countType}} count = 0;
                    {{marked}}
                    return (int)count;
                }
            }
            """;

        return AnalyzerTestRunner.VerifyAsync<ManualBitCountAnalyzer>(code);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("int", "int")]
    [InlineData("uint", "long")]
    [InlineData("long", "long")]
    [InlineData("int", "long")]
    public Task Reports_ForSupportedValueAndCountTypes(string valueType, string countType)
    {
        return VerifyAsync(valueType, countType, "value != 0", "count += value & 1;", "value >>= 1;", true);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("short", "int")]
    [InlineData("byte", "int")]
    [InlineData("int", "double")]
    public Task Silent_ForUnsupportedValueOrCountTypes(string valueType, string countType)
    {
        return VerifyAsync(valueType, countType, "value != 0", "count += value & 1;", "value >>= 1;", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheZeroIsOnTheLeft()
    {
        return VerifyAsync("int", "int", "0 != value", "count += value & 1;", "value >>= 1;", true);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("value != 1")]
    [InlineData("value != 0L")]
    [InlineData("value != other")]
    [InlineData("1 != value")]
    [InlineData("0 != 0")]
    [InlineData("value > 0")]
    public Task Silent_WhenTheConditionIsNotAComparisonWithZero(string condition)
    {
        return VerifyAsync("int", "int", condition, "count += value & 1;", "value >>= 1;", false);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("count = value & 1;")]
    [InlineData("count += value & 2;")]
    [InlineData("count += other & 1;")]
    [InlineData("count += (value & 1);")]
    [InlineData("count += value | 1;")]
    [InlineData("count += value;")]
    [InlineData("count -= value & 1;")]
    [InlineData("if (value > 1) { count++; }")]
    public Task Silent_WhenTheFirstStatementIsNotTheLowBitAddition(string first)
    {
        return VerifyAsync("int", "int", "value != 0", first, "value >>= 1;", false);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("value >>= 2;")]
    [InlineData("value >>= shift;")]
    [InlineData("value <<= 1;")]
    [InlineData("other >>= 1;")]
    [InlineData("value = value >> 1;")]
    [InlineData("value++;")]
    public Task Silent_WhenTheSecondStatementIsNotAShiftByOne(string second)
    {
        return VerifyAsync("int", "int", "value != 0", "count += value & 1;", second, false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueAndCountAreNotLocalsOrParameters()
    {
        return AnalyzerTestRunner.VerifyAsync<ManualBitCountAnalyzer>(
            """
            class C
            {
                private int _value;

                private int _count;

                int FromField(int seed)
                {
                    int count = 0;
                    while (_value != 0)
                    {
                        count += _value & 1;
                        _value >>= 1;
                    }

                    return count + seed;
                }

                int CountField(int value)
                {
                    while (value != 0)
                    {
                        _count += value & 1;
                        value >>= 1;
                    }

                    return value;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopIsAnEmbeddedStatement()
    {
        return AnalyzerTestRunner.VerifyAsync<ManualBitCountAnalyzer>(
            """
            class C
            {
                int Count(int value, bool flag)
                {
                    int count = 0;
                    if (flag)
                        while (value != 0)
                        {
                            count += value & 1;
                            value >>= 1;
                        }

                    return count;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopBodyIsNotABlock()
    {
        return AnalyzerTestRunner.VerifyAsync<ManualBitCountAnalyzer>(
            """
            class C
            {
                int Count(int value)
                {
                    int count = 0;
                    while (value != 0)
                        count += value & 1;

                    return count;
                }
            }
            """);
    }
}
