using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RuntimeRegexToGeneratedAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePatternIsAConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeRegexToGeneratedAnalyzer>("""
                           using System.Text.RegularExpressions;
                           class C
                           {
                               {|E128123:private static readonly Regex Digits = new Regex("[0-9]+");|}
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePatternIsAComputedValue()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeRegexToGeneratedAnalyzer>("""
                           using System.Text.RegularExpressions;
                           class C
                           {
                               private static readonly Regex Digits;

                               static C()
                               {
                                   Digits = new Regex(GetPattern());
                               }

                               static string GetPattern()
                               {
                                   return "[0-9]+";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenRegexIsAUserType()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeRegexToGeneratedAnalyzer>("""
                           class Regex
                           {
                               public Regex(string pattern)
                               {
                               }
                           }

                           class C
                           {
                               private static readonly Regex Digits = new Regex("[0-9]+");
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTypeIsQualified()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeRegexToGeneratedAnalyzer>("""
                           class C
                           {
                               {|E128123:private static readonly System.Text.RegularExpressions.Regex Digits = new System.Text.RegularExpressions.Regex("[0-9]+");|}
                           }
                           """);
    }
}
