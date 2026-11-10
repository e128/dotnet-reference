using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessOutputValidatorE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ProcessOutputValidatorAnalyzer, ProcessOutputValidatorCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsSystemAndIoUsings_WhenFullyQualified()
    {
        const string source = """
                              using System.Diagnostics;
                              class C
                              {
                                  string M(Process process, string outputFile)
                                  {
                                      process.WaitForExit();
                                      return {|E128101:System.IO.File.ReadAllText(outputFile)|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.Diagnostics;
                                 using System.IO;

                                 class C
                                 {
                                     string M(Process process, string outputFile)
                                     {
                                         process.WaitForExit();
                                         if (!File.Exists(outputFile)) throw new InvalidOperationException($"Expected output file not created: {outputFile}");
                                         return System.IO.File.ReadAllText(outputFile);
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_UsesFileInfoExists_WhenPathIsFullName()
    {
        const string source = """
                              using System.Diagnostics;
                              using System.IO;
                              class C
                              {
                                  string M(Process process, FileInfo outputFile)
                                  {
                                      process.WaitForExit();
                                      return {|E128101:File.ReadAllText(outputFile.FullName)|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.Diagnostics;
                                 using System.IO;
                                 class C
                                 {
                                     string M(Process process, FileInfo outputFile)
                                     {
                                         process.WaitForExit();
                                         if (!outputFile.Exists) throw new InvalidOperationException($"Expected output file not created: {outputFile.FullName}");
                                         return File.ReadAllText(outputFile.FullName);
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
