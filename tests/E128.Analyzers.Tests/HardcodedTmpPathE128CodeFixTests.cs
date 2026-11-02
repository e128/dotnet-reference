using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HardcodedTmpPathE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<HardcodedTmpPathE128Analyzer, HardcodedTmpPathCodeFixProvider>(source, fixedCode);

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

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_KeepsSubdirectory_WhenLiteralNamesOne()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M()
                {
                    var path = {|E128023:"/tmp/cache"|};
                }
            }
            """,
            """
            class C
            {
                void M()
                {
                    var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cache");
                }
            }
            """);
    }
}
