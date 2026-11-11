using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnjustifiedSuppressionAttributeAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task NullJustification_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UnjustifiedSuppressionAttributeAnalyzer>("""
                           using System.Diagnostics.CodeAnalysis;
                           class C
                           {
                               [{|E128173:SuppressMessage("Design", "CA1000", Justification = null)|}]
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SuppressMessageWithoutJustification_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UnjustifiedSuppressionAttributeAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<UnjustifiedSuppressionAttributeAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<UnjustifiedSuppressionAttributeAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<UnjustifiedSuppressionAttributeAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<UnjustifiedSuppressionAttributeAnalyzer>("""
                           using System;
                           class C
                           {
                               [Obsolete("x")]
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExcludeFromCodeCoverageWithoutJustificationProperty_Silent()
    {
        return new CSharpAnalyzerTest<UnjustifiedSuppressionAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = """
                       namespace System.Diagnostics.CodeAnalysis
                       {
                           public sealed class ExcludeFromCodeCoverageAttribute : System.Attribute { }
                       }

                       namespace App
                       {
                           using System.Diagnostics.CodeAnalysis;

                           [ExcludeFromCodeCoverage]
                           class C { }
                       }
                       """,
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20
        }.RunAsync(TestContext.Current.CancellationToken);
    }
}
