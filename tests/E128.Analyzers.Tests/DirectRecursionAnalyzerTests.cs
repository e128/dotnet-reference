using System.Threading.Tasks;
using E128.Analyzers.Maintainability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DirectRecursionAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<DirectRecursionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotReport_WhenAnotherInstanceCallsTheMethod()
    {
        return VerifyAsync("""
                           class C
                           {
                               int Down(int n) => n <= 0 ? 0 : new C().Down(n - 1);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheThisInstanceCallsTheMethod()
    {
        return VerifyAsync("""
                           class C
                           {
                               int {|E128199:Down|}(int n) => n <= 0 ? 0 : this.Down(n - 1);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAMethodCallsItself()
    {
        return VerifyAsync("""
                           class C
                           {
                               int {|E128199:Down|}(int n)
                               {
                                   return n <= 0 ? 0 : Down(n - 1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnAnExpressionBody()
    {
        return VerifyAsync("""
                           class C
                           {
                               int {|E128199:Down|}(int n) => n <= 0 ? 0 : Down(n - 1);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnAStaticMethod()
    {
        return VerifyAsync("""
                           static class C
                           {
                               static int {|E128199:Down|}(int n) => n <= 0 ? 0 : Down(n - 1);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAMethodCallsAnotherMethod()
    {
        return VerifyAsync("""
                           class C
                           {
                               int Down(int n) => n <= 0 ? 0 : n + Other(n - 1);

                               int Other(int n) => n;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallSitsInADeferredCallback()
    {
        return VerifyAsync("""
                           class Queue
                           {
                               public void Post(System.Action action) => action();
                           }
                           class C
                           {
                               private readonly Queue _queue = new();

                               void Start() => _queue.Post(() => Start());
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTwoMethodsDoNotCallThemselves()
    {
        return VerifyAsync("""
                           class C
                           {
                               int First(int n) => Second(n);

                               int Second(int n) => n;
                           }
                           """);
    }
}
