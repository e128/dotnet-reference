using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UntrackedSkipAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<UntrackedSkipAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithXunit
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SkipWithReasonOnly_Fires()
    {
        return VerifyAsync("""
                           using Xunit;
                           public class T
                           {
                               [Fact({|E128168:Skip = "flaky on CI"|})]
                               public void A() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TheorySkipEmpty_Fires()
    {
        return VerifyAsync("""
                           using Xunit;
                           public class T
                           {
                               [Theory({|E128168:Skip = ""|})]
                               [InlineData(1)]
                               public void A(int x) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SkipWithTrackerReference_Silent()
    {
        return VerifyAsync("""
                           using Xunit;
                           public class T
                           {
                               [Fact(Skip = "AB#4521 flaky on CI")]
                               public void A() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FactWithoutSkip_Silent()
    {
        return VerifyAsync("""
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A() { }

                               [Fact(DisplayName = "named")]
                               public void B() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonTestAttributeWithSkip_Silent()
    {
        return VerifyAsync("""
                           using System;
                           sealed class MarkerAttribute : Attribute
                           {
                               public string Skip { get; set; } = "";
                           }
                           class C
                           {
                               [Marker(Skip = "later")]
                               void A() { }
                           }
                           """);
    }
}
