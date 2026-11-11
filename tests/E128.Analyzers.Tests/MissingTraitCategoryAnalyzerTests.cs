using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MissingTraitCategoryAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithXunit };

    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("xunit.v3.core", "3.2.2")]);

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_FactMethod_WithoutTrait()
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [{|E128073:Fact|}]
                               public void Should_Work() { }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_TheoryMethod_WithoutTrait()
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [{|E128073:Theory|}]
                               [InlineData(1)]
                               public void Should_Work(int x) { _ = x; }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenTraitPresent()
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [Fact]
                               [Trait("Category", "CI")]
                               public void Should_Work() { }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenTraitPresentOnTheory()
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [Theory]
                               [InlineData(1)]
                               [Trait("Category", "CI")]
                               public void Should_Work(int x) { _ = x; }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenNonTestMethod()
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>("""
                           public sealed class MyClass
                           {
                               public void NotATest() { }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenTraitHasDifferentCategoryValue()
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [Fact]
                               [Trait("Category", "Podman")]
                               public void Should_Work() { }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_MultipleMethodsMissing()
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenClassLevelTraitPresent()
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>("""
                           using Xunit;
                           [Trait("Category", "CI")]
                           public sealed class MyTests
                           {
                               [Fact]
                               public void Should_Work() { }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenTheTraitIsQualified()
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>("""
                           using Xunit;
                           public sealed class MyTests
                           {
                               [Fact]
                               [Xunit.Trait("Category", "CI")]
                               public void Should_Work() { }
                           }
                           """, options: Options);
    }
}
