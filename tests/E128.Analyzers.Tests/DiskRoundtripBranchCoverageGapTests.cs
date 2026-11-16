using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DiskRoundtripBranchCoverageGapTests
{
    private static readonly AnalyzerTestOptions NoCompilerDiagnostics =
        new() { CompilerDiagnostics = CompilerDiagnostics.None };

    private static Task VerifyAsync(string members, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>(
            "using System;\nusing System.IO;\nclass C\n{\n" + members + "}\n",
            options: options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnExpressionBodyHoldsAWriteAndAReadWithoutAStatement()
    {
        return VerifyAsync("""
                static object Combine(object first, object second) => first;

                object M(string path) => Combine(File.Create(path), File.ReadAllBytes(path));

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAFileCallCannotBindToASystemIoOverload()
    {
        return VerifyAsync("""
                string M(string path)
                {
                    File.WriteAllText(path);
                    return File.ReadAllText(path);
                }

            """, NoCompilerDiagnostics);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAFileStreamConstructorCannotBindToASystemIoOverload()
    {
        return VerifyAsync("""
                string M(string path, string content)
                {
                    File.WriteAllText(path, content);
                    using (var stream = new FileStream(path)) { }
                    return content;
                }

            """, NoCompilerDiagnostics);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverOfAnInstanceCallHasNoType()
    {
        return VerifyAsync("""
                void M(string path)
                {
                    Helper.OpenWrite();
                    Helper.OpenRead();
                }

                static void Helper() { }

            """, NoCompilerDiagnostics);
    }
}
