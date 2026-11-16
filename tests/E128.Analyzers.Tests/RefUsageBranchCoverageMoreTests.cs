using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RefUsageBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResultIsIncrementedOrDecremented()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsToBitCastAnalyzer>("""
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value = 1;
                    ++Unsafe.As<int, uint>(ref value);
                    --Unsafe.As<int, uint>(ref value);
                    Unsafe.As<int, uint>(ref value)++;
                    Unsafe.As<int, uint>(ref value)--;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAParenthesizedResultIsAssigned()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsToBitCastAnalyzer>("""
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value = 1;
                    (Unsafe.As<int, uint>(ref value)) = 2u;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheResultIsReadByValue()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsToBitCastAnalyzer>("""
            using System.Runtime.CompilerServices;
            class C
            {
                static void Use(uint number)
                {
                }

                void M()
                {
                    int value = 1;
                    uint copy;
                    copy = {|E128112:Unsafe.As<int, uint>(ref value)|};
                    var negated = -{|E128112:Unsafe.As<int, int>(ref value)|};
                    var text = {|E128112:Unsafe.As<int, uint>(ref value)|}.ToString();
                    Use({|E128112:Unsafe.As<int, uint>(ref value)|});
                }
            }
            """);
    }
}
