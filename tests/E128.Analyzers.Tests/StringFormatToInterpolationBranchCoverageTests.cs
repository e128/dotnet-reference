using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringFormatToInterpolationBranchCoverageTests
{
    private static Task VerifyAnalyzerAsync(string code, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<StringFormatToInterpolationAnalyzer>(code, expected: null, options: options);
    }

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<StringFormatToInterpolationAnalyzer, StringFormatToInterpolationCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<StringFormatToInterpolationAnalyzer, StringFormatToInterpolationCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenFormatIsCalledWithoutAReceiver()
    {
        return VerifyAnalyzerAsync("""
            using static System.String;
            class C
            {
                void M(int n)
                {
                    var s = Format("Value: {0}", n);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenTheMethodIsNotFormat()
    {
        return VerifyAnalyzerAsync("""
            class C
            {
                void M(int n)
                {
                    var s = string.Concat("Value: ", n);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenFormatHasOnlyTheFormatString()
    {
        return VerifyAnalyzerAsync("""
            class C
            {
                void M()
                {
                    var s = string.Format("Value");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenTheCallDoesNotBind()
    {
        return VerifyAnalyzerAsync(
            """
            class C
            {
                void M()
                {
                    var s = string.Format(1, 2);
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenFormatBelongsToAnotherType()
    {
        return VerifyAnalyzerAsync("""
            class Formatter
            {
                public static string Format(string template, int value)
                {
                    return template + value;
                }
            }

            class C
            {
                void M(int n)
                {
                    var s = Formatter.Format("Value: {0}", n);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenTheFormatIsACompositeFormatVariable()
    {
        return VerifyAnalyzerAsync("""
            using System.Globalization;
            using System.Text;
            class C
            {
                void M(CompositeFormat format, int n)
                {
                    var s = string.Format(CultureInfo.InvariantCulture, format, n);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenTheConstantFormatIsNull()
    {
        return VerifyAnalyzerAsync("""
            class C
            {
                const string? Template = null;

                void M(int n)
                {
                    var s = string.Format(Template, n);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheFormatSpecifier()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(double price, int count)
                {
                    var s = {|E128015:string.Format("Price {0:N2} for {1}", price, count)|};
                }
            }
            """,
            """
            class C
            {
                void M(double price, int count)
                {
                    var s = $"Price {price:N2} for {count}";
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_EscapesTabsQuotesBackslashesAndCarriageReturns()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(int n)
                {
                    var s = {|E128015:string.Format("tab\t quote\" back\\ cr\r {0}", n)|};
                }
            }
            """,
            """
            class C
            {
                void M(int n)
                {
                    var s = $"tab\t quote\" back\\ cr\r {n}";
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAPlaceholderIsNotClosed()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(int n)
                {
                    var s = {|E128015:string.Format("value {0", n)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAPlaceholderPointsPastTheArguments()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(int n)
                {
                    var s = {|E128015:string.Format("value {1}", n)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAPlaceholderIsNotAnIndex()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(int n)
                {
                    var s = {|E128015:string.Format("value {name} {-1}", n)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAPlaceholderCarriesAlignment()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(int n)
                {
                    var s = {|E128015:string.Format("value {0,5}", n)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAClosingBraceIsAtTheEnd()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(int n)
                {
                    var s = {|E128015:string.Format("{0} }", n)|};
                }
            }
            """);
    }
}
