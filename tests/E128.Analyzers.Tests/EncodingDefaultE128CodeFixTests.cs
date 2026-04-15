using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EncodingDefaultE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<EncodingDefaultAnalyzer, EncodingDefaultCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EncodingDefault_CodeFix_ReplacesWithUtf8()
    {
        const string source = """
            using System.Text;
            class C
            {
                void M()
                {
                    var bytes = {|E128006:Encoding.Default|}.GetBytes("test");
                }
            }
            """;

        const string fixedCode = """
            using System.Text;
            class C
            {
                void M()
                {
                    var bytes = Encoding.UTF8.GetBytes("test");
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MultipleOccurrences_FixAll_ReplacesAll()
    {
        const string source = """
            using System.Text;
            class C
            {
                void M()
                {
                    var a = {|E128006:Encoding.Default|}.GetBytes("x");
                    var b = {|E128006:Encoding.Default|}.GetString(new byte[] { 0 });
                }
            }
            """;

        const string fixedCode = """
            using System.Text;
            class C
            {
                void M()
                {
                    var a = Encoding.UTF8.GetBytes("x");
                    var b = Encoding.UTF8.GetString(new byte[] { 0 });
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
