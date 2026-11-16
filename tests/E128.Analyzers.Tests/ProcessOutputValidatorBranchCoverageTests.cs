using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessOutputValidatorBranchCoverageTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputValidatorAnalyzer>(
            "using System;\nusing System.Diagnostics;\nusing System.IO;\nclass C\n{\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReadHappensBeforeTheWait()
    {
        return VerifyAsync("""
                string M(Process process, string outputFile)
                {
                    var text = File.ReadAllText(outputFile);
                    process.WaitForExit();
                    return text;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePathHasNoSimpleRoot()
    {
        return VerifyAsync("""
                static string Path() => "out.txt";

                string M(Process process)
                {
                    process.WaitForExit();
                    var a = File.ReadAllText("out.txt");
                    var b = File.ReadAllText(Path());
                    return a + b;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAFileInfoExistsCheckPrecedesTheRead()
    {
        return VerifyAsync("""
                string? M(Process process, FileInfo output)
                {
                    process.WaitForExit();
                    if (!output.Exists)
                    {
                        return null;
                    }

                    return File.ReadAllText(output.FullName);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheExistsCheckFollowsTheRead()
    {
        return VerifyAsync("""
                string M(Process process, string outputFile)
                {
                    process.WaitForExit();
                    var text = {|E128101:File.ReadAllText(outputFile)|};
                    return File.Exists(outputFile) ? text : string.Empty;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheExistsCheckGuardsAnotherPath()
    {
        return VerifyAsync("""
                string M(Process process, string outputFile, string other)
                {
                    process.WaitForExit();
                    if (!File.Exists(other))
                    {
                        return string.Empty;
                    }

                    return {|E128101:File.ReadAllText(outputFile)|};
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAQualifiedFileReadAfterAWaitOnADerivedProcess()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputValidatorAnalyzer>("""
            using System.Diagnostics;

            class Child : Process
            {
                string M(string outputFile)
                {
                    WaitForExit();
                    return {|E128101:System.IO.File.ReadAllText(outputFile)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheWaitIsNotOnAProcess()
    {
        return VerifyAsync("""
                sealed class Worker
                {
                    public void WaitForExit() { }
                }

                string M(Worker worker, string outputFile)
                {
                    worker.WaitForExit();
                    return File.ReadAllText(outputFile);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheWaitIsAnotherProcessMethodOrAnInvokedDelegate()
    {
        return VerifyAsync("""
                string M(Process process, Func<Action> factory, string outputFile)
                {
                    process.Start();
                    factory()();
                    return File.ReadAllText(outputFile);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForReadsThatAreNotOnFile()
    {
        return VerifyAsync("""
                string M(Process process, StreamReader reader, string outputFile)
                {
                    process.WaitForExit();
                    return reader.ReadToEnd() + Path.GetFileName(outputFile);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAMethodWithoutAProcessWait()
    {
        return VerifyAsync("""
                string M(string outputFile) => File.ReadAllText(outputFile);

            """);
    }
}
