using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnjustifiedSuppressionAttributeAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<UnjustifiedSuppressionAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SuppressMessageWithoutJustification_Fires()
    {
        return VerifyAsync("""
                           using System.Diagnostics.CodeAnalysis;
                           class C
                           {
                               [{|E128173:SuppressMessage("Design", "CA1000")|}]
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExcludeFromCodeCoverageBare_Fires()
    {
        return VerifyAsync("""
                           using System.Diagnostics.CodeAnalysis;
                           [{|E128173:ExcludeFromCodeCoverage|}]
                           class C
                           {
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EmptyJustification_Fires()
    {
        return VerifyAsync("""
                           using System.Diagnostics.CodeAnalysis;
                           class C
                           {
                               [{|E128173:SuppressMessage("Design", "CA1000", Justification = " ")|}]
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WithJustification_Silent()
    {
        return VerifyAsync("""
                           using System.Diagnostics.CodeAnalysis;
                           [ExcludeFromCodeCoverage(Justification = "Entry point only.")]
                           class C
                           {
                               [SuppressMessage("Design", "CA1000", Justification = "Factory shape required by the API.")]
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OtherAttribute_Silent()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               [Obsolete("x")]
                               void M() { }
                           }
                           """);
    }
}
