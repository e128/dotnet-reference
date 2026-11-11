using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConstantOperandAssertionAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithXunit };

    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantOperandAssertionAnalyzer_ReportsAssertion_WhenBothOperandsAreLiterals()
    {
        return AnalyzerTestRunner.VerifyAsync<ConstantOperandAssertionAnalyzer>("""
                           using Xunit;

                           public static class Subject
                           {
                               public static void Verify()
                               {
                                   {|E128104:Assert.Equal("alpha", "alpha")|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantOperandAssertionAnalyzer_ReportsAssertion_WhenBothOperandsAreNumericLiterals()
    {
        return AnalyzerTestRunner.VerifyAsync<ConstantOperandAssertionAnalyzer>("""
                           using Xunit;

                           public static class Subject
                           {
                               public static void Verify()
                               {
                                   {|E128104:Assert.Equal(2, 2)|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantOperandAssertionAnalyzer_ReportsNothing_WhenOneOperandIsNotConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<ConstantOperandAssertionAnalyzer>("""
                           using Xunit;

                           public static class Subject
                           {
                               public sealed class Result
                               {
                                   public string Name { get; init; } = string.Empty;
                               }

                               public static void Verify(Result result)
                               {
                                   Assert.Equal("alpha", result.Name);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantOperandAssertionAnalyzer_ReportsNothing_WhenAnOperandIsANamedConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<ConstantOperandAssertionAnalyzer>("""
                           using Xunit;

                           public static class Subject
                           {
                               private const int Expected = 2;

                               public static void Verify(int actual)
                               {
                                   Assert.Equal(Expected, actual);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantOperandAssertionAnalyzer_ReportsNothing_WhenADynamicSkipCarriesALiteralReason()
    {
        return AnalyzerTestRunner.VerifyAsync<ConstantOperandAssertionAnalyzer>("""
                           using Xunit;

                           public static class Subject
                           {
                               public static void Verify()
                               {
                                   Assert.Skip("the fixture needs a network share");
                               }
                           }
                           """, options: Options);
    }
}
