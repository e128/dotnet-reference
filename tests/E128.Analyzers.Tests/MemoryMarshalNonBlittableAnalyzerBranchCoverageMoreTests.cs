using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MemoryMarshalNonBlittableAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<MemoryMarshalNonBlittableAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInvokedExpressionHasNoSimpleName()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                int M(Func<Func<int>> factory)
                {
                    return factory()();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsDynamic()
    {
        return VerifyAsync("""
            class C
            {
                object M(dynamic reader)
                {
                    return reader.Read<bool>(0);
                }
            }
            """);
    }
}
