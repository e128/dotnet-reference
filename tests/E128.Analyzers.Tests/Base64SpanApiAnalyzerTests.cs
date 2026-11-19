using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Base64SpanApiAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForToBase64String()
    {
        return AnalyzerTestRunner.VerifyAsync<Base64SpanApiAnalyzer>("""
            class C
            {
                string M(byte[] value) => {|E128252:System.Convert.ToBase64String(value)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForFromBase64String()
    {
        return AnalyzerTestRunner.VerifyAsync<Base64SpanApiAnalyzer>("""
            class C
            {
                byte[] M(string value) => {|E128252:System.Convert.FromBase64String(value)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnotherConvertMember()
    {
        return AnalyzerTestRunner.VerifyAsync<Base64SpanApiAnalyzer>("""
            class C
            {
                string M(byte[] value) => System.Convert.ToHexString(value);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserTypeWithTheSameMethodName()
    {
        return AnalyzerTestRunner.VerifyAsync<Base64SpanApiAnalyzer>("""
            class Converter
            {
                public string ToBase64String(byte[] value) => string.Empty;
            }

            class C
            {
                string M(Converter converter, byte[] value) => converter.ToBase64String(value);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFrameworkHasNoSpanApi()
    {
        return AnalyzerTestRunner.VerifyAsync<Base64SpanApiAnalyzer>(
            """
            class C
            {
                string M(byte[] value) => System.Convert.ToBase64String(value);
            }
            """,
            options: new AnalyzerTestOptions { References = ReferenceAssemblies.NetStandard.NetStandard20 });
    }
}
