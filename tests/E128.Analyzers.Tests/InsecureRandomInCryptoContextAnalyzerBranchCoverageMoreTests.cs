using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InsecureRandomInCryptoContextAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<InsecureRandomInCryptoContextAnalyzer>(code, expected: null, options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_CallsOnARandomReachedThroughPropertiesThatAreNotRandomShared()
    {
        return VerifyAsync("""
            using System;
            class Holder
            {
                public Random Rng { get; } = new();
                public static Random Instance { get; } = new();
            }
            class Other
            {
                public static Random Shared { get; } = new();
            }
            class C
            {
                void M(Holder holder)
                {
                    var token = {|E128075:holder.Rng.Next()|};
                    var pin = {|E128075:Holder.Instance.Next()|};
                    var otp = {|E128075:Other.Shared.Next()|};
                }
            }
            """);
    }
}
