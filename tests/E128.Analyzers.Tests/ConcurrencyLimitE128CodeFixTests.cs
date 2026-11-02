using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrencyLimitE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<ConcurrencyLimitAnalyzer, ConcurrencyLimitCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task MaxDegreeOfParallelism_Zero_ReplacesWithProcessorCount()
    {
        const string source = """
                              using System;
                              using System.Threading.Tasks;
                              class C
                              {
                                  void M(ParallelOptions options)
                                  {
                                      options.MaxDegreeOfParallelism = {|E128040:0|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     void M(ParallelOptions options)
                                     {
                                         options.MaxDegreeOfParallelism = Environment.ProcessorCount;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
