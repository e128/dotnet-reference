using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Utf8StringParseAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<Utf8StringParseAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheOnlyUtf8OverloadIsGeneric()
    {
        return VerifyAsync("""
            using System;
            using System.Text;

            readonly struct Number
            {
                public static Number Parse(string text) => default;

                public static Number Parse<T>(ReadOnlySpan<byte> utf8) => default;
            }

            class C
            {
                Number M(byte[] bytes)
                {
                    return Number.Parse(Encoding.UTF8.GetString(bytes));
                }
            }
            """);
    }
}
