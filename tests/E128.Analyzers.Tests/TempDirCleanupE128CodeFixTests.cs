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
        // xUnit v3 declares both members with ValueTask.
        const string source = """
                              using System.IO;
                              using System.Threading.Tasks;

                              interface IAsyncLifetime
                              {
                                  ValueTask InitializeAsync();
                                  ValueTask DisposeAsync();
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
                                     ValueTask InitializeAsync();
                                     ValueTask DisposeAsync();
                                 }

                                 class MyTests
                                 : IAsyncLifetime
                                 {
                                     private readonly string _path = Path.Combine(Path.GetTempPath(), "test");

                                 public ValueTask InitializeAsync() => ValueTask.CompletedTask;

                                     public ValueTask DisposeAsync()
                                     {
                                         Directory.Delete(_path, true);
                                         return ValueTask.CompletedTask;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_CodeFix_DeletesTheMemberThatHoldsThePath()
    {
        // The field is not named _path, so the fix reads the name from the diagnostic.
        const string source = """
                              using System.IO;
                              using System.Threading.Tasks;

                              interface IAsyncLifetime
                              {
                                  ValueTask InitializeAsync();
                                  ValueTask DisposeAsync();
                              }

                              class {|E128054:MyTests|}
                              {
                                  private readonly string _root = Path.Combine(Path.GetTempPath(), "test");
                              }
                              """;

        const string fixedCode = """
                                 using System.IO;
                                 using System.Threading.Tasks;

                                 interface IAsyncLifetime
                                 {
                                     ValueTask InitializeAsync();
                                     ValueTask DisposeAsync();
                                 }

                                 class MyTests
                                 : IAsyncLifetime
                                 {
                                     private readonly string _root = Path.Combine(Path.GetTempPath(), "test");

                                 public ValueTask InitializeAsync() => ValueTask.CompletedTask;

                                     public ValueTask DisposeAsync()
                                     {
                                         Directory.Delete(_root, true);
                                         return ValueTask.CompletedTask;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NotOffered_WhenTheTempPathGoesToALocal()
    {
        const string source = """
                              using System;
                              using System.IO;

                              public class {|E128054:Fixture|}
                              {
                                  public Fixture()
                                  {
                                      var root = Path.GetTempPath();
                                      _ = root;
                                  }
                              }
                              """;

        // A local is not a member, so the generated Dispose call could not reach it.
        var test = new CSharpCodeFixTest<TempDirCleanupAnalyzer, TempDirCleanupCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            NumberOfFixAllInDocumentIterations = 0
        };

        return test.RunAsync();
    }
}
