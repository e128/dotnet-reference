using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessOutputValidatorAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ProcessOutputValidatorAnalyzer_FlagsOutputUse_WhenNoExistenceCheck()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputValidatorAnalyzer>("""
                           using System.Diagnostics;
                           using System.IO;
                           class C
                           {
                               string M(Process process, string outputFile)
                               {
                                   process.WaitForExit();
                                   return {|E128101:File.ReadAllText(outputFile)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ProcessOutputValidatorAnalyzer_DoesNotFlag_WhenExistenceCheckPresent()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputValidatorAnalyzer>("""
                           using System.Diagnostics;
                           using System.IO;
                           class C
                           {
                               string M(Process process, string outputFile)
                               {
                                   process.WaitForExit();
                                   if (!File.Exists(outputFile))
                                   {
                                       return null;
                                   }
                                   return File.ReadAllText(outputFile);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ProcessOutputValidatorAnalyzer_DoesNotFlag_WhenAsyncWaitHasCheck()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputValidatorAnalyzer>("""
                           using System.Diagnostics;
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task<string> M(Process process, string outputFile)
                               {
                                   await process.WaitForExitAsync();
                                   if (!File.Exists(outputFile))
                                   {
                                       return null;
                                   }
                                   return File.ReadAllText(outputFile);
                               }
                           }
                           """);
    }
}
