using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessOutputValidatorCodeFixBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesFileExistsForAMemberThatIsNotFullName()
    {
        return CodeFixTestRunner.RunAsync<ProcessOutputValidatorAnalyzer, ProcessOutputValidatorCodeFixProvider>(
            """
            using System.Diagnostics;
            using System.IO;
            class Options { public string Output { get; set; } = ""; }
            class C
            {
                string M(Process process, Options options)
                {
                    process.WaitForExit();
                    return {|E128101:File.ReadAllText(options.Output)|};
                }
            }
            """,
            """
            using System;
            using System.Diagnostics;
            using System.IO;
            class Options { public string Output { get; set; } = ""; }
            class C
            {
                string M(Process process, Options options)
                {
                    process.WaitForExit();
                    if (!File.Exists(options.Output)) throw new InvalidOperationException($"Expected output file not created: {options.Output}");
                    return File.ReadAllText(options.Output);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_InsertsTheGuardAfterTheLastWaitBeforeTheRead()
    {
        return CodeFixTestRunner.RunAsync<ProcessOutputValidatorAnalyzer, ProcessOutputValidatorCodeFixProvider>(
            """
            using System.Diagnostics;
            using System.IO;
            class C
            {
                string M(Process first, Process second, string outputFile)
                {
                    first.WaitForExit();
                    second.WaitForExit();
                    var note = "x";
                    return note + {|E128101:File.ReadAllText(outputFile)|};
                }
            }
            """,
            """
            using System;
            using System.Diagnostics;
            using System.IO;
            class C
            {
                string M(Process first, Process second, string outputFile)
                {
                    first.WaitForExit();
                    second.WaitForExit();
                    if (!File.Exists(outputFile)) throw new InvalidOperationException($"Expected output file not created: {outputFile}");
                    var note = "x";
                    return note + File.ReadAllText(outputFile);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheWaitSitsInAnOuterBlock()
    {
        return CodeFixTestRunner.NoFixAsync<ProcessOutputValidatorAnalyzer, ProcessOutputValidatorCodeFixProvider>(
            """
            using System.Diagnostics;
            using System.IO;
            class C
            {
                string M(Process process, string outputFile, bool flag)
                {
                    process.WaitForExit();
                    if (flag)
                    {
                        return {|E128101:File.ReadAllText(outputFile)|};
                    }

                    return string.Empty;
                }
            }
            """);
    }
}
