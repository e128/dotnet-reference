using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RuntimeRegexToGeneratedAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<RuntimeRegexToGeneratedAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePatternIsAConstant()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
                           class C
                           {
                               {|E128123:private static readonly System.Text.RegularExpressions.Regex Digits = new System.Text.RegularExpressions.Regex("[0-9]+");|}
                           }
                           """);
    }
}
