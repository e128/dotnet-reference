using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimingSafeComparisonCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesSequenceEqualWithFixedTimeCompare()
    {
        const string source = """
                              using System.Linq;

                              class C
                              {
                                  bool M(byte[] computedTag, byte[] expectedTag)
                                  {
                                      return {|E128157:computedTag.SequenceEqual(expectedTag)|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Linq;
                                 using System.Security.Cryptography;

                                 class C
                                 {
                                     bool M(byte[] computedTag, byte[] expectedTag)
                                     {
                                         return CryptographicOperations.FixedTimeEquals(computedTag, expectedTag);
                                     }
                                 }
                                 """;

        return CodeFixTestRunner.RunAsync<TimingSafeComparisonAnalyzer,
            TimingSafeComparisonCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesStaticSequenceEqualWithFixedTimeCompare()
    {
        const string source = """
                              using System;

                              class C
                              {
                                  bool M(ReadOnlySpan<byte> computedTag, ReadOnlySpan<byte> expectedTag)
                                  {
                                      return {|E128157:MemoryExtensions.SequenceEqual(computedTag, expectedTag)|};
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

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAnOperandMayBeNull()
    {
        return CodeFixTestRunner.NoFixAsync<TimingSafeComparisonAnalyzer, TimingSafeComparisonCodeFixProvider>(
            """
            #nullable enable
            using System.Linq;

            class C
            {
                bool M(byte[]? computedTag, byte[] expectedTag)
                {
                    return {|E128157:computedTag.SequenceEqual(expectedTag)|};
                }
            }
            """);
    }
}
