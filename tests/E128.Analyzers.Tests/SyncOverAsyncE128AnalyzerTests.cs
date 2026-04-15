using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SyncOverAsyncE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SyncOverAsyncAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskResult_InMethod_FiresE128008()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            class C
            {
                void M()
                {
                    var t = Task.FromResult(42);
                    var x = {|E128008:t.Result|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAwaiterGetResult_InMethod_FiresE128008()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            class C
            {
                void M()
                {
                    var t = Task.FromResult(42);
                    var x = t.{|E128008:GetAwaiter|}().GetResult();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskResult_InStaticMain_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            class Program
            {
                static void Main()
                {
                    var t = Task.FromResult(42);
                    var x = t.Result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskResult_InDispose_DoesNotFire()
    {
        return VerifyAsync("""
            using System;
            using System.Threading.Tasks;
            class C : IDisposable
            {
                public void Dispose()
                {
                    var t = Task.FromResult(42);
                    var x = t.Result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskResult_InAsyncMethod_FiresE128008()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            class C
            {
                async Task M()
                {
                    var t = Task.FromResult(42);
                    var x = {|E128008:t.Result|};
                    await Task.CompletedTask;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonTaskResult_PropertyNamedResult_DoesNotFire()
    {
        return VerifyAsync("""
            class MyClass
            {
                public int Result { get; set; }
            }
            class C
            {
                void M()
                {
                    var obj = new MyClass();
                    var x = obj.Result;
                }
            }
            """);
    }
}
