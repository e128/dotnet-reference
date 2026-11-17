using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InsecureRandomInCryptoContextOuterShadowTests
{
    /// <summary>
    ///     A type in the enclosing namespace wins over a using directive. After the fix adds the
    ///     cryptography using, the plain name still binds to the source type, so no fix is offered.
    /// </summary>
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("class RandomNumberGenerator { }")]
    [InlineData("static class RandomNumberGenerator { public static int GetInt32(int fromInclusive, int toExclusive) => 4; }")]
    public Task NoFix_WhenAGlobalNamespaceTypeShadowsRandomNumberGenerator(string shadow)
    {
        return CodeFixTestRunner.NoFixAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(
            $$"""
              using System;

              {{shadow}}

              class C
              {
                  int M()
                  {
                      var token = {|E128075:Random.Shared|}.Next(1, 7);
                      return token;
                  }
              }
              """);
    }

    /// <summary>
    ///     The shadow check must not block the fix when the existing cryptography using already
    ///     resolves the plain name to the BCL type.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_WhenNothingShadowsRandomNumberGenerator()
    {
        return CodeFixTestRunner.RunAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(
            """
            using System;
            using System.Security.Cryptography;

            class Other { }

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
            class Other { }

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
}
