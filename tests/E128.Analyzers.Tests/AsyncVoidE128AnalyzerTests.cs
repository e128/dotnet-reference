using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncVoidE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncVoid_NonEventHandler_FiresE128007()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncVoidAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async void {|E128007:DoWork|}()
                               {
                                   await Task.Delay(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncVoid_EventHandler_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncVoidAnalyzer>("""
                           using System;
                           using System.Threading.Tasks;
                           class C
                           {
                               async void OnClick(object sender, EventArgs e)
                               {
                                   await Task.Delay(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncTask_Method_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncVoidAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task DoWork()
                               {
                                   await Task.Delay(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncVoid_CustomEventArgs_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncVoidAnalyzer>("""
                           using System;
                           using System.Threading.Tasks;
                           class MyEventArgs : EventArgs { }
                           class C
                           {
                               async void OnCustom(object sender, MyEventArgs e)
                               {
                                   await Task.Delay(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SyncVoid_Method_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncVoidAnalyzer>("""
                           class C
                           {
                               void DoWork() { }
                           }
                           """);
    }
}
