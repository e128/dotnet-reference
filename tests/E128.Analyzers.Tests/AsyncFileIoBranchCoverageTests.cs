using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncFileIoBranchCoverageTests
{
    private static AnalyzerTestOptions Options => new() { MarkupOptions = MarkupOptions.UseFirstDescriptor };

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncFileIoAnalyzer>("using System;\nusing System.IO;\nusing System.Threading.Tasks;\n" + code, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForASyncLambdaInsideAnAsyncMethod()
    {
        return VerifyAsync("""
            class C
            {
                async Task M(string path)
                {
                    await Task.Yield();
                    Action read = () => {|E128092:File.ReadAllText(path)|};
                    read();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InAConstructor()
    {
        return VerifyAsync("""
            class C
            {
                public C(string path)
                {
                    {|E128092:File.ReadAllText(path)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InASyncLocalFunctionAndASyncAnonymousMethod()
    {
        return VerifyAsync("""
            class C
            {
                void M(string path)
                {
                    void Local() => {|E128092:File.ReadAllText(path)|};
                    Action anon = delegate { {|E128092:File.ReadAllText(path)|}; };
                    Local();
                    anon();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_InsideAsyncLocalFunctionAndAsyncLambdas()
    {
        return VerifyAsync("""
            class C
            {
                void M(string path)
                {
                    async Task Local() { await Task.Yield(); File.ReadAllText(path); }
                    Func<Task> simple = async () => { await Task.Yield(); File.ReadAllText(path); };
                    Func<int, Task> parenthesized = async (int x) => { await Task.Yield(); File.ReadAllText(path); };
                    Func<int, Task> single = async x => { await Task.Yield(); File.ReadAllText(path); };
                    Func<Task> anon = async delegate { await Task.Yield(); File.ReadAllText(path); };
                    _ = (Local(), simple, parenthesized, single, anon);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnUnqualifiedCallThroughUsingStatic()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncFileIoAnalyzer>("""
            using static System.IO.File;

            class C
            {
                void M(string path)
                {
                    {|E128092:ReadAllText(path)|};
                }
            }
            """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCalledNameAlreadyEndsInAsync()
    {
        return VerifyAsync("""
            class C
            {
                void M(string path)
                {
                    _ = File.ReadAllTextAsync(path);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForNameofAndDelegateInvocations()
    {
        return VerifyAsync("""
            class C
            {
                void M(string path, Func<Action> factory)
                {
                    var name = nameof(path);
                    factory()();
                    _ = name;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAFileTypeOutsideSystemIo()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncFileIoAnalyzer>("""
            namespace Other
            {
                static class File
                {
                    public static string ReadAllText(string path) => path;
                    public static System.Threading.Tasks.Task<string> ReadAllTextAsync(string path) => null!;
                }

                class C
                {
                    void M(string path)
                    {
                        File.ReadAllText(path);
                    }
                }
            }
            """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAFileMethodWithoutAnAsyncSibling()
    {
        return VerifyAsync("""
            class C
            {
                bool M(string path) => File.Exists(path);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForANonFileTypeInSystemIo()
    {
        return VerifyAsync("""
            class C
            {
                string M(string path) => Path.GetFullPath(path);
            }
            """);
    }
}
