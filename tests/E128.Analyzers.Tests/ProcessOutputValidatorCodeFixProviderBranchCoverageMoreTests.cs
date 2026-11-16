using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessOutputValidatorCodeFixProviderBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_InsertsTheGuardAfterAnAsyncWaitAndSkipsStatementsAfterTheRead()
    {
        return CodeFixTestRunner.RunAsync<ProcessOutputValidatorAnalyzer, ProcessOutputValidatorCodeFixProvider>(
            """
            using System.Diagnostics;
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                static void Log()
                {
                }

                async Task<string> M(Process process, string outputFile)
                {
                    await process.WaitForExitAsync();
                    Log();
                    var text = {|E128101:File.ReadAllText(outputFile)|};
                    Log();
                    return text;
                }
            }
            """,
            """
            using System;
            using System.Diagnostics;
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                static void Log()
                {
                }

                async Task<string> M(Process process, string outputFile)
                {
                    await process.WaitForExitAsync();
                    if (!File.Exists(outputFile)) throw new InvalidOperationException($"Expected output file not created: {outputFile}");
                    Log();
                    var text = File.ReadAllText(outputFile);
                    Log();
                    return text;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheReadIsInAnExpressionBodiedMethod()
    {
        return CodeFixTestRunner.NoFixAsync<ProcessOutputValidatorAnalyzer, ProcessOutputValidatorCodeFixProvider>(
            """
            using System.Diagnostics;
            using System.IO;
            class C
            {
                string M(Process process, string outputFile) =>
                    process.WaitForExitAsync().IsCompleted ? {|E128101:File.ReadAllText(outputFile)|} : string.Empty;
            }
            """);
    }
}
