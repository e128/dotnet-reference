using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PoolRentCapacityGuardAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRentCallPassesTwoArguments()
    {
        return AnalyzerTestRunner.VerifyAsync<PoolRentCapacityGuardAnalyzer>("""
            class WidePool
            {
                public byte[] Rent(int size, int extra) => new byte[size + extra];
            }

            class C
            {
                byte[] M(WidePool pool, int size) => pool.Rent(size, 2);
            }
            """);
    }
}
