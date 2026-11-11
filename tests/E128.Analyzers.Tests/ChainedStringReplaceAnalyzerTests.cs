using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ChainedStringReplaceAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ChainOfTwoOrMoreReplaceCalls()
    {
        return AnalyzerTestRunner.VerifyAsync<ChainedStringReplaceAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ChainedStringReplaceAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ChainedStringReplaceAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ChainedStringReplaceAnalyzer>("""
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
