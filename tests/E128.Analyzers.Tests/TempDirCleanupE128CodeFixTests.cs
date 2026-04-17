using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TempDirCleanupE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<TempDirCleanupAnalyzer, TempDirCleanupCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            NumberOfFixAllIterations = 1
        };
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_CodeFix_AddsIAsyncLifetime()
    {
        const string source = """
                              using System.IO;
                              using System.Threading.Tasks;

                              interface IAsyncLifetime
                              {
                                  Task InitializeAsync();
                                  Task DisposeAsync();
                              }

                              class {|E128054:MyTests|}
                              {
                                  private readonly string _path = Path.Combine(Path.GetTempPath(), "test");
                              }
                              """;

        const string fixedCode = """
                                 using System.IO;
                                 using System.Threading.Tasks;

                                 interface IAsyncLifetime
                                 {
                                     Task InitializeAsync();
                                     Task DisposeAsync();
                                 }

                                 class MyTests
                                 : IAsyncLifetime
                                 {
                                     private readonly string _path = Path.Combine(Path.GetTempPath(), "test");

                                 public Task InitializeAsync() => Task.CompletedTask;

                                     public Task DisposeAsync()
                                     {
                                         Directory.Delete(_path, true);
                                         return Task.CompletedTask;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
