using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EncodingDefaultE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<EncodingDefaultAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EncodingDefault_InMethod_FiresE128006()
    {
        return VerifyAsync("""
            using System.Text;
            class C
            {
                void M()
                {
                    var bytes = {|E128006:Encoding.Default|}.GetBytes("test");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EncodingUtf8_InMethod_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Text;
            class C
            {
                void M()
                {
                    var bytes = Encoding.UTF8.GetBytes("test");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EncodingAscii_InMethod_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Text;
            class C
            {
                void M()
                {
                    var bytes = Encoding.ASCII.GetBytes("test");
                }
            }
            """);
    }
}
