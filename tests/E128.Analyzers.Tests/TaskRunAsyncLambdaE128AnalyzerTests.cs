using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskRunAsyncLambdaE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task TaskRun_AsyncLambda_FiresE128036()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskRunAsyncLambdaAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   await {|E128036:Task.Run(async () => await Task.Delay(1))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskRun_SyncLambda_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskRunAsyncLambdaAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   await Task.Run(() => 42);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskRun_AsyncAnonymousDelegate_FiresE128036()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskRunAsyncLambdaAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   await {|E128036:Task.Run(async delegate { await Task.Delay(1); })|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonTaskRun_AsyncLambda_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskRunAsyncLambdaAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               static Task Run(System.Func<Task> action) => action();
                               async Task M()
                               {
                                   await Run(async () => await Task.Delay(1));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskRun_ParenthesizedAsyncLambdaWithBlock_FiresE128036()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskRunAsyncLambdaAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   await {|E128036:Task.Run(async () =>
                                   {
                                       await Task.Delay(1);
                                       await Task.Delay(2);
                                   })|};
                               }
                           }
                           """);
    }
}
