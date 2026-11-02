using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Utf8StringParseCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<Utf8StringParseAnalyzer, Utf8StringParseCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PassesTheBytesAndTheCurrentCulture()
    {
        return VerifyFixAsync(
            """
            using System.Text;
            class C
            {
                int M(byte[] bytes)
                {
                    return int.Parse({|E128131:Encoding.UTF8.GetString(bytes)|});
                }
            }
            """,
            """
            using System.Globalization;
            using System.Text;
            class C
            {
                int M(byte[] bytes)
                {
                    return int.Parse(bytes, CultureInfo.CurrentCulture);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheOutParameterOnTryParse()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Text;
            class C
            {
                bool M(ReadOnlySpan<byte> bytes)
                {
                    return int.TryParse({|E128131:Encoding.UTF8.GetString(bytes)|}, out var value);
                }
            }
            """,
            """
            using System;
            using System.Text;
            class C
            {
                bool M(ReadOnlySpan<byte> bytes)
                {
                    return int.TryParse(bytes, out var value);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AppendsTheProviderAfterAnExistingArgument()
    {
        return VerifyFixAsync(
            """
            using System.Globalization;
            using System.Text;
            class C
            {
                int M(byte[] bytes)
                {
                    return int.Parse({|E128131:Encoding.UTF8.GetString(bytes)|},
                        NumberStyles.Integer, CultureInfo.InvariantCulture);
                }
            }
            """,
            """
            using System.Globalization;
            using System.Text;
            class C
            {
                int M(byte[] bytes)
                {
                    return int.Parse(bytes,
                        NumberStyles.Integer, CultureInfo.InvariantCulture);
                }
            }
            """);
    }
}
