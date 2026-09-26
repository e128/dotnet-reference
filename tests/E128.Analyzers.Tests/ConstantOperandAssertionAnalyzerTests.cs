using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConstantOperandAssertionAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConstantOperandAssertionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithXunit
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantOperandAssertionAnalyzer_ReportsAssertion_WhenBothOperandsAreLiterals()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class Subject
                           {
                               public static void Verify()
                               {
                                   {|E128104:Assert.Equal("alpha", "alpha")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantOperandAssertionAnalyzer_ReportsAssertion_WhenBothOperandsAreNumericLiterals()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class Subject
                           {
                               public static void Verify()
                               {
                                   {|E128104:Assert.Equal(2, 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantOperandAssertionAnalyzer_ReportsNothing_WhenOneOperandIsNotConstant()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantOperandAssertionAnalyzer_ReportsNothing_WhenAnOperandIsANamedConstant()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class Subject
                           {
                               private const int Expected = 2;

                               public static void Verify(int actual)
                               {
                                   Assert.Equal(Expected, actual);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantOperandAssertionAnalyzer_ReportsNothing_WhenADynamicSkipCarriesALiteralReason()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class Subject
                           {
                               public static void Verify()
                               {
                                   Assert.Skip("the fixture needs a network share");
                               }
                           }
                           """);
    }
}
