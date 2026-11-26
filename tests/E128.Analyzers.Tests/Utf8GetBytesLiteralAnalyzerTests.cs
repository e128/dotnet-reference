using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Utf8GetBytesLiteralAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return VerifyAsync(code, LanguageVersion.Latest);
    }

    private static Task VerifyAsync(string code, LanguageVersion languageVersion)
    {
        return AnalyzerTestRunner.VerifyAsync<Utf8GetBytesLiteralAnalyzer>(
            code,
            options: new AnalyzerTestOptions { LanguageVersion = languageVersion });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStringIsALiteral()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               byte[] M()
                               {
                                   return {|E128141:Encoding.UTF8.GetBytes("abc")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStringIsNotConstant()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               byte[] M(string value)
                               {
                                   return Encoding.UTF8.GetBytes(value);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLanguageVersionIsBeforeCSharp11()
    {
        return VerifyAsync(
            """
            using System.Text;
            class C
            {
                byte[] M()
                {
                    return Encoding.UTF8.GetBytes("abc");
                }
            }
            """,
            LanguageVersion.CSharp10);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheOverloadTakesARange()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               byte[] M()
                               {
                                   return Encoding.UTF8.GetBytes("abc", 0, 1);
                               }
                           }
                           """);
    }
}
