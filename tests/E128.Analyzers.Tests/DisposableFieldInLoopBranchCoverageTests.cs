using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DisposableFieldInLoopBranchCoverageTests
{
    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>($$"""
            using System;
            class Resource : IDisposable { public void Dispose() { } }
            class C
            {
                IDisposable current = new Resource();
                IDisposable other = new Resource();
                void M(int count, bool flag)
                {
                    {{body}}
                }
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("for (int i = 0; i < count; i++) { {|E128193:current|} = new Resource(); }")]
    [InlineData("do { {|E128193:current|} = new Resource(); count--; } while (count > 0);")]
    public Task Reports_WhenALoopOverwritesADisposableField(string body)
    {
        return VerifyAsync(body);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAssignmentAppearsInTheForInitializer()
    {
        return VerifyAsync("for (current = new Resource(); count > 0; count--) { }");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAssignmentAppearsInTheWhileCondition()
    {
        return VerifyAsync("while ((current = new Resource()) == null) { }");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenASeparateLocalFunctionOwnsTheAssignment()
    {
        return VerifyAsync("for (int i = 0; i < count; i++) { void Set() { current = new Resource(); } Set(); }");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyAnotherFieldIsDisposedAfterTheAssignment()
    {
        return VerifyAsync("for (int i = 0; i < count; i++) { {|E128193:current|} = new Resource(); other.Dispose(); }");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheDisposeBelongsToAnUninvokedLambda()
    {
        return VerifyAsync("for (int i = 0; i < count; i++) { {|E128193:current|} = new Resource(); Action release = () => current.Dispose(); GC.KeepAlive(release); }");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("if (flag) break;")]
    [InlineData("if (flag) return;")]
    [InlineData("if (flag) throw new InvalidOperationException();")]
    public Task Reports_WhenAnExitCanSkipTheLaterDispose(string exit)
    {
        return VerifyAsync($$"""for (int i = 0; i < count; i++) { {|E128193:current|} = new Resource(); {{exit}} current.Dispose(); }""");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFieldImplementsAsyncDisposable()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>("""
            using System;
            using System.Threading.Tasks;
            class Resource : IAsyncDisposable { public ValueTask DisposeAsync() => default; }
            class C
            {
                IAsyncDisposable current = new Resource();
                void M(int count)
                {
                    for (int i = 0; i < count; i++) { {|E128193:current|} = new Resource(); }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopDisposesTheAsyncFieldBeforeReplacement()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>("""
            using System;
            using System.Threading.Tasks;
            class Resource : IAsyncDisposable { public ValueTask DisposeAsync() => default; }
            class C
            {
                IAsyncDisposable current = new Resource();
                async Task M(int count)
                {
                    for (int i = 0; i < count; i++)
                    {
                        await current.DisposeAsync();
                        current = new Resource();
                    }
                }
            }
            """);
    }
}
