using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncVoidE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<AsyncVoidAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncVoid_NonEventHandler_FiresE128007()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
            class C
            {
                void DoWork() { }
            }
            """);
    }
}
