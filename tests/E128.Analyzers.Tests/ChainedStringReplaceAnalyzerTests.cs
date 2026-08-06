using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ChainedStringReplaceAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ChainedStringReplaceAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ChainOfTwoOrMoreReplaceCalls()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(string value)
                               {
                                   var result = {|E128098:value.Replace('\n', ' ').Replace('\r', ' ')|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_SingleReplaceCall()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(string value)
                               {
                                   var result = value.Replace('\n', ' ');
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ReplaceUntilStableLoop()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(string value)
                               {
                                   {|E128098:while (value.Contains("--", StringComparison.Ordinal))
                                   {
                                       value = value.Replace("--", "-", StringComparison.Ordinal);
                                   }|}
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_NonStringReceiver()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               void M(StringBuilder value)
                               {
                                   value.Replace('\n', ' ').Replace('\r', ' ');
                               }
                           }
                           """);
    }
}
