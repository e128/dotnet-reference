using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the wrapper types, I/O owners, and rewrites of the FromResult rule and its code fix.
/// </summary>
public sealed class TaskFromResultSyncIoBranchCoverageTests
{
    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<TaskFromResultSyncIoAnalyzer>(source, expected: null, options);
    }

    private static Task FixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<TaskFromResultSyncIoAnalyzer, TaskFromResultSyncIoCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForValueTaskFromResult()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               ValueTask<int> M(string path)
                               {
                                   File.WriteAllText(path, "x");
                                   return {|E128028:ValueTask.FromResult(1)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForABaseStreamCopy()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task<int> M(Stream source, Stream target)
                               {
                                   source.CopyTo(target);
                                   return {|E128028:Task.FromResult(1)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForASynchronousHttpSend()
    {
        return VerifyAsync("""
                           using System.Net.Http;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task<int> M(HttpClient client, HttpRequestMessage request)
                               {
                                   client.Send(request);
                                   return {|E128028:Task.FromResult(1)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFromResultBelongsToAnotherType()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class Results
                           {
                               public static Task<int> FromResult(int value) => Task.CompletedTask.ContinueWith(_ => value);
                           }

                           class C
                           {
                               Task<int> M(string path)
                               {
                                   File.WriteAllText(path, "x");
                                   return Results.FromResult(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFromResultIsInAPropertyBody()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly string _path = "x";

                               Task<string> Text => Task.FromResult(File.ReadAllText(_path));
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallsBelongToLookAlikeTypes()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Text;
                           using System.Threading.Tasks;
                           class File
                           {
                               public static string ReadAllText(string path) => path;

                               public static Task<string> ReadAllTextAsync(string path) => Task.FromResult(path);
                           }

                           class C
                           {
                               Task<string> M(string path, StreamReader reader, StringBuilder builder)
                               {
                                   var first = File.ReadAllText(path);
                                   var second = reader.Read();
                                   builder.Append(first);
                                   return Task.FromResult(first);
                               }
                           }
                           """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSyncCallDoesNotBind()
    {
        return VerifyAsync(
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                Task<int> M()
                {
                    File.ReadAllText(missing);
                    unknown.ReadAllText();
                    return Task.FromResult(1);
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesAnExpressionBodiedMethod()
    {
        return FixAsync(
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                Task<string> M(string path) => {|E128028:Task.FromResult(File.ReadAllText(path))|};
            }
            """,
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                async Task<string> M(string path) => await File.ReadAllTextAsync(path);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesAValueTaskAndAStreamCopy()
    {
        return FixAsync(
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                ValueTask<int> M(string path, Stream source, Stream target)
                {
                    source.CopyTo(target);
                    File.WriteAllText(path, "x");
                    return {|E128028:ValueTask.FromResult(1)|};
                }
            }
            """,
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                async ValueTask<int> M(string path, Stream source, Stream target)
                {
                    await source.CopyToAsync(target);
                    await File.WriteAllTextAsync(path, "x");
                    return 1;
                }
            }
            """);
    }
}
