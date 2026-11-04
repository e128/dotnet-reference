using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimingSafeComparisonCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesEqualityWithFixedTimeCompare()
    {
        const string source = """
                              using System;

                              class C
                              {
                                  bool M(ReadOnlySpan<byte> computedTag, ReadOnlySpan<byte> expectedTag)
                                  {
                                      return {|E128157:computedTag == expectedTag|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.Security.Cryptography;

                                 class C
                                 {
                                     bool M(ReadOnlySpan<byte> computedTag, ReadOnlySpan<byte> expectedTag)
                                     {
                                         return CryptographicOperations.FixedTimeEquals(computedTag, expectedTag);
                                     }
                                 }
                                 """;

        return CodeFixTestRunner.RunAsync<TimingSafeComparisonAnalyzer,
            TimingSafeComparisonCodeFixProvider>(source, fixedCode);
    }
}
