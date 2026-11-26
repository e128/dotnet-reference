using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TempDirCleanupE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<TempDirCleanupAnalyzer, TempDirCleanupCodeFixProvider>(
            source,
            fixedCode,
            behaviors: CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            options: new CodeFixTestOptions { FixAllIterations = 1 });
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

                                 class MyTests : IAsyncLifetime
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

                                 class MyTests : IAsyncLifetime
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
        return CodeFixTestRunner.RunAsync<TempDirCleanupAnalyzer, TempDirCleanupCodeFixProvider>(
            source,
            source,
            behaviors: CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            options: new CodeFixTestOptions { FixAllInDocumentIterations = 0 });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_DoesNotDuplicateAnExistingLifetimeMethod()
    {
        // A second InitializeAsync would be CS0111. The existing member satisfies the interface.
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

                                  public ValueTask InitializeAsync() => ValueTask.CompletedTask;
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

                                 class MyTests : IAsyncLifetime
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
    public Task CodeFix_DoesNotReformatTheExistingMembers()
    {
        // The fix touches the header and appends members. It must not reindent the field.
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

                                 class MyTests : IAsyncLifetime
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
}
