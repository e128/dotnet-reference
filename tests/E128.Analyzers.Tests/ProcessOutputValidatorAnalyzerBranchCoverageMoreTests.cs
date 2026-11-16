using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessOutputValidatorAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputValidatorAnalyzer>(
            "using System;\nusing System.Diagnostics;\nclass C\n{\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFileReadHasNoArguments()
    {
        return VerifyAsync("""
                static class File
                {
                    public static string ReadAllText() => string.Empty;

                    public static bool Exists() => true;
                }

                string M(Process process)
                {
                    process.WaitForExit();
                    _ = File.Exists();
                    return File.ReadAllText();
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForReadsThroughAnotherQualifierOrACall()
    {
        return VerifyAsync("""
                sealed class Reader
                {
                    public string ReadAllText(string path) => path;
                }

                sealed class Holder
                {
                    public Reader Reader { get; } = new Reader();
                }

                string M(Process process, Holder holder, string outputFile)
                {
                    process.WaitForExit();
                    var first = holder.Reader.ReadAllText(outputFile);
                    var second = Create().ReadAllText(outputFile);
                    return first + second;
                }

                static Reader Create() => new Reader();

            """);
    }
}
