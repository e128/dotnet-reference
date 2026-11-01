using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ManualBitCountCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<ManualBitCountAnalyzer, ManualBitCountCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_CastsAnIntValueToUnsigned()
    {
        return VerifyFixAsync(
            """
            class C
            {
                int Count(int value)
                {
                    int count = 0;
                    {|E128134:while (value != 0)
                    {
                        count += value & 1;
                        value >>= 1;
                    }|}
                    return count;
                }
            }
            """,
            """
            using System.Numerics;

            class C
            {
                int Count(int value)
                {
                    int count = 0;
                    count += BitOperations.PopCount((uint)value);
                    return count;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_CastsALongValueToUnsigned()
    {
        return VerifyFixAsync(
            """
            class C
            {
                long Count(long value)
                {
                    long count = 0;
                    {|E128134:while (value != 0)
                    {
                        count += value & 1;
                        value >>= 1;
                    }|}
                    return count;
                }
            }
            """,
            """
            using System.Numerics;

            class C
            {
                long Count(long value)
                {
                    long count = 0;
                    count += BitOperations.PopCount((ulong)value);
                    return count;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsAnUnsignedValueUncast()
    {
        return VerifyFixAsync(
            """
            class C
            {
                long Count(uint value)
                {
                    long count = 0;
                    {|E128134:while (value != 0)
                    {
                        count += value & 1;
                        value >>= 1;
                    }|}
                    return count;
                }
            }
            """,
            """
            using System.Numerics;

            class C
            {
                long Count(uint value)
                {
                    long count = 0;
                    count += BitOperations.PopCount(value);
                    return count;
                }
            }
            """);
    }
}
