using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class WhenAnyResultUnobservedAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<WhenAnyResultUnobservedAnalyzer>(
            "using System;\nusing System.Threading.Tasks;\n"
            + "class Box { public int t; }\n"
            + "class C\n{\n    void M(Task a, Task b, Box box, Func<Action> factory, Func<Task, Task, Task<Task>> WhenAny)\n    {\n" + body + "\n    }\n}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnInvokedDelegateResultAndADelegateNamedWhenAny()
    {
        return VerifyAsync("""
                                   factory()();
                                   _ = WhenAny(a, b);
                                   WhenAny(a, b);
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheWhenAnyCallBindsDynamically()
    {
        return AnalyzerTestRunner.VerifyAsync<WhenAnyResultUnobservedAnalyzer>("""
                                                                               using System.Threading.Tasks;
                                                                               class C
                                                                               {
                                                                                   void M(Task a)
                                                                                   {
                                                                                       dynamic tasks = new object();
                                                                                       tasks.WhenAny(a);
                                                                                   }
                                                                               }
                                                                               """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyAMemberWithTheLocalsNameIsRead()
    {
        return VerifyAsync("""
                                   var t = {|E128183:Task.WhenAny(a, b)|};
                                   _ = box.t;
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsDeclaredOutsideAnyBlock()
    {
        return AnalyzerTestRunner.VerifyAsync<WhenAnyResultUnobservedAnalyzer>(
            """
            using System.Threading.Tasks;

            Task a = Task.CompletedTask;
            Task b = Task.CompletedTask;
            var first = Task.WhenAny(a, b);
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
