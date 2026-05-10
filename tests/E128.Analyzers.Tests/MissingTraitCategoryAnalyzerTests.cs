using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MissingTraitCategoryAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("xunit.v3.core", "3.2.2")]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<MissingTraitCategoryAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithXunit
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_FactMethod_WithoutTrait()
    {
        return VerifyAsync("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [{|E128073:Fact|}]
                               public void Should_Work() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_TheoryMethod_WithoutTrait()
    {
        return VerifyAsync("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [{|E128073:Theory|}]
                               [InlineData(1)]
                               public void Should_Work(int x) { _ = x; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenTraitPresent()
    {
        return VerifyAsync("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [Fact]
                               [Trait("Category", "CI")]
                               public void Should_Work() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenTraitPresentOnTheory()
    {
        return VerifyAsync("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [Theory]
                               [InlineData(1)]
                               [Trait("Category", "CI")]
                               public void Should_Work(int x) { _ = x; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenNonTestMethod()
    {
        return VerifyAsync("""
                           public sealed class MyClass
                           {
                               public void NotATest() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenTraitHasDifferentCategoryValue()
    {
        return VerifyAsync("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [Fact]
                               [Trait("Category", "Docker")]
                               public void Should_Work() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_MultipleMethodsMissing()
    {
        return VerifyAsync("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [{|E128073:Fact|}]
                               public void First() { }

                               [Fact]
                               [Trait("Category", "CI")]
                               public void Second() { }

                               [{|E128073:Theory|}]
                               [InlineData(1)]
                               public void Third(int x) { _ = x; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenClassLevelTraitPresent()
    {
        return VerifyAsync("""
                           using Xunit;
                           [Trait("Category", "CI")]
                           public sealed class MyTests
                           {
                               [Fact]
                               public void Should_Work() { }
                           }
                           """);
    }
}
