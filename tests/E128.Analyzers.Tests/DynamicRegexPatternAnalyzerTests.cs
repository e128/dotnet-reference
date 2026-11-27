using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DynamicRegexPatternAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePatternIsAVariable()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicRegexPatternAnalyzer>("""
                           using System.Text.RegularExpressions;

                           class C
                           {
                               bool Run(string value, string pattern)
                               {
                                   return [|Regex.IsMatch(value, pattern)|];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePatternIsConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicRegexPatternAnalyzer>("""
                           using System.Text.RegularExpressions;

                           class C
                           {
                               bool Run(string value)
                               {
                                   return Regex.IsMatch(value, "a+");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallPassesATimeout()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicRegexPatternAnalyzer>("""
                           using System;
                           using System.Text.RegularExpressions;

                           class C
                           {
                               bool Run(string value, string pattern)
                               {
                                   return Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheOptionsCarryNonBacktracking()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicRegexPatternAnalyzer>("""
                           using System.Text.RegularExpressions;

                           class C
                           {
                               bool Run(string value, string pattern)
                               {
                                   return Regex.IsMatch(value, pattern, RegexOptions.NonBacktracking);
                               }
                           }
                           """);
    }
}
