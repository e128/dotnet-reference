using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StackAllocSafetyAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConstantSizeIsNotAnInt()
    {
        return AnalyzerTestRunner.VerifyAsync<StackAllocSafetyAnalyzer>("""
            using System;
            class C
            {
                void M()
                {
                    const byte size = 16;
                    Span<byte> buffer = stackalloc byte[size];
                }
            }
            """);
    }
}
