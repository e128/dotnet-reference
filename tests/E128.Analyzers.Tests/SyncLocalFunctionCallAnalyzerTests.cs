using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SyncLocalFunctionCallAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { MarkupOptions = MarkupOptions.UseFirstDescriptor };

    /// <summary>
    ///     AC-1: E128096 fires on async local function invoked via .Result in a synchronous method.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task SyncLocalFunctionCallAnalyzer_Fires_OnLocalFunctionResult()
    {
        return AnalyzerTestRunner.VerifyAsync<SyncLocalFunctionCallAnalyzer>("""
                           using System.Threading.Tasks;

                           class C
                           {
                               void M()
                               {
                                   async Task<string> FetchAsync() => await Task.FromResult("value");
                                   _ = {|E128096:FetchAsync()|}.Result;
                               }
                           }
                           """, options: Options);
    }

    /// <summary>
    ///     AC-2: E128096 fires on async local function invoked via .Wait() in a synchronous method.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task SyncLocalFunctionCallAnalyzer_Fires_OnLocalFunctionWait()
    {
        return AnalyzerTestRunner.VerifyAsync<SyncLocalFunctionCallAnalyzer>("""
                           using System.Threading.Tasks;

                           class C
                           {
                               void M()
                               {
                                   async Task LoadAsync() { await Task.CompletedTask; }
                                   {|E128096:LoadAsync()|}.Wait();
                               }
                           }
                           """, options: Options);
    }

    /// <summary>
    ///     AC-3: E128096 does not fire when the containing method is already async.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task SyncLocalFunctionCallAnalyzer_DoesNotFire_WhenContainingMethodAsync()
    {
        return AnalyzerTestRunner.VerifyAsync<SyncLocalFunctionCallAnalyzer>("""
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M()
                               {
                                   async Task<string> FetchAsync() => await Task.FromResult("value");
                                   _ = FetchAsync().Result;
                                   await Task.CompletedTask;
                               }
                           }
                           """, options: Options);
    }

    /// <summary>
    ///     AC-4: E128096 does not fire on non-async local function.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task SyncLocalFunctionCallAnalyzer_DoesNotFire_OnNonAsyncLocalFunction()
    {
        return AnalyzerTestRunner.VerifyAsync<SyncLocalFunctionCallAnalyzer>("""
                           using System.Threading.Tasks;

                           class C
                           {
                               void M()
                               {
                                   Task<string> FetchAsync() => Task.FromResult("value");
                                   _ = FetchAsync().Result;
                               }
                           }
                           """, options: Options);
    }

    /// <summary>
    ///     AC-5: E128096 does not fire on method declarations (VSTHRD002 covers them).
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task SyncLocalFunctionCallAnalyzer_DoesNotFire_OnMethodDeclaration()
    {
        return AnalyzerTestRunner.VerifyAsync<SyncLocalFunctionCallAnalyzer>("""
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task<string> FetchAsync() => await Task.FromResult("value");
                               void M()
                               {
                                   _ = FetchAsync().Result;
                               }
                           }
                           """, options: Options);
    }

    /// <summary>
    ///     AC-6: E128096 fires on Task&lt;T&gt; return type.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task SyncLocalFunctionCallAnalyzer_Fires_OnTaskTReturnType()
    {
        return AnalyzerTestRunner.VerifyAsync<SyncLocalFunctionCallAnalyzer>("""
                           using System.Threading.Tasks;

                           class C
                           {
                               void M()
                               {
                                   async Task<int> GetCountAsync() => await Task.FromResult(42);
                                   _ = {|E128096:GetCountAsync()|}.Result;
                               }
                           }
                           """, options: Options);
    }
}
