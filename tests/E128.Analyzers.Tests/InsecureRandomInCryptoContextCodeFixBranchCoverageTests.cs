using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InsecureRandomInCryptoContextCodeFixBranchCoverageTests
{
    private static string Source(string expression, string usings)
    {
        return $$"""
            {{usings}}

            class C
            {
                int M()
                {
                    var token = {{expression}};
                    return 0;
                }
            }
            """;
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("{|E128075:Random.Shared|}.Next((byte)1, (byte)7)", "RandomNumberGenerator.GetInt32((byte)1, (byte)7)")]
    [InlineData("{|E128075:Random.Shared|}.Next((sbyte)1, (sbyte)7)", "RandomNumberGenerator.GetInt32((sbyte)1, (sbyte)7)")]
    [InlineData("{|E128075:Random.Shared|}.Next((short)1, (short)7)", "RandomNumberGenerator.GetInt32((short)1, (short)7)")]
    [InlineData("{|E128075:Random.Shared|}.Next((ushort)1, (ushort)7)", "RandomNumberGenerator.GetInt32((ushort)1, (ushort)7)")]
    [InlineData("{|E128075:Random.Shared|}.Next('a', 'z')", "RandomNumberGenerator.GetInt32('a', 'z')")]
    [InlineData("{|E128075:Random.Shared|}.Next()", "RandomNumberGenerator.GetInt32(int.MaxValue)")]
    public Task RandomSharedNext_FixedToGetInt32_ForSmallIntegerConstants(string original, string replacement)
    {
        return CodeFixTestRunner.RunAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(
            Source(original, "using System;"),
            Source(replacement, "using System;\nusing System.Security.Cryptography;"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheDiagnosticIsNotARandomSharedNextCall()
    {
        return CodeFixTestRunner.NoFixAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(
            """
            using System;
            class C
            {
                void M(Random random)
                {
                    var token = {|E128075:Random.Shared|};
                    var secret = {|E128075:Random.Shared|}.NextDouble();
                    var nonce = ({|E128075:Random.Shared|}).Next(5);
                }
            }
            """);
    }
}
