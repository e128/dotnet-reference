using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskRunAsyncLambdaAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenARunMethodTakesNoArguments()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskRunAsyncLambdaAnalyzer>("""
            class Worker
            {
                public void Run()
                {
                }
            }

            class C
            {
                void M(Worker worker)
                {
                    worker.Run();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRunCallDoesNotBind()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskRunAsyncLambdaAnalyzer>(
            """
            using System.Threading.Tasks;
            class C
            {
                async Task M()
                {
                    await Task.Run(async () => await Task.Delay(1), 5, 6);
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAsyncLambdasPassedToOtherRunMethods()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskRunAsyncLambdaAnalyzer>("""
            using System;
            using System.Threading.Tasks;

            class Other
            {
                public static Task Run(Func<int, Task> action) => action(1);
            }

            class C
            {
                Func<Func<Task>, Task> Run { get; } = action => action();

                async Task M()
                {
                    await Other.Run(async count => await Task.Delay(count));
                    await this.Run(async () => await Task.Delay(1));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForATaskTypeInAnotherNamespace()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskRunAsyncLambdaAnalyzer>("""
            using System;

            namespace Mine
            {
                class Task
                {
                    public static System.Threading.Tasks.Task Run(Func<System.Threading.Tasks.Task> action) => action();
                }
            }

            class C
            {
                void M()
                {
                    _ = Mine.Task.Run(async () => await System.Threading.Tasks.Task.Delay(1));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForSynchronousAnonymousMethodsAndMethodGroups()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskRunAsyncLambdaAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                static void Work()
                {
                }

                async Task M()
                {
                    await Task.Run(delegate { });
                    await Task.Run(Work);
                    await Task.Run(delegate { return 1; });
                }
            }
            """);
    }
}
