using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HardcodedTmpPathE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<HardcodedTmpPathE128Analyzer, HardcodedTmpPathCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesHardcodedTmpWithGetTempPath()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M()
                {
                    var path = {|E128023:"/tmp"|};
                }
            }
            """,
            """
            class C
            {
                void M()
                {
                    var path = System.IO.Path.GetTempPath();
                }
            }
            """);
    }
}
