using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InsecureRandomInCryptoContextCodeFixProviderBranchCoverageGapTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheExistingCryptographyUsing()
    {
        return CodeFixTestRunner.RunAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                int M()
                {
                    var token = {|E128075:Random.Shared|}.Next(1, 7);
                    return token;
                }
            }
            """,
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                int M()
                {
                    var token = RandomNumberGenerator.GetInt32(1, 7);
                    return token;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheNextCallBindsToAnExtensionMethod()
    {
        return CodeFixTestRunner.NoFixAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(
            """
            using System;

            static class RandomExtensions
            {
                public static int Next(this Random random, string label) => label.Length;
            }

            class C
            {
                int M()
                {
                    var token = {|E128075:Random.Shared|}.Next("label");
                    return token;
                }
            }
            """);
    }
}
