using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FunctionPointerDelegateLifetimeAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<FunctionPointerDelegateLifetimeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheDelegateIsALocal()
    {
        return VerifyAsync("""
                           using System;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M(Action callback)
                               {
                                   IntPtr pointer = {|E128128:Marshal.GetFunctionPointerForDelegate(callback)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheDelegateIsAField()
    {
        return VerifyAsync("""
                           using System;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               private readonly Action _callback = () => { };

                               void M()
                               {
                                   IntPtr pointer = Marshal.GetFunctionPointerForDelegate(_callback);
                               }
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheDelegateIsATemporary()
    {
        return VerifyAsync("""
                           using System;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               delegate void Callback();

                               static void Target()
                               {
                               }

                               void M()
                               {
                                   IntPtr first = {|E128128:Marshal.GetFunctionPointerForDelegate(new Callback(Target))|};
                                   IntPtr second = {|E128128:Marshal.GetFunctionPointerForDelegate((Action)Target)|};
                                   IntPtr third = {|E128128:Marshal.GetFunctionPointerForDelegate<Action>(() => { })|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsStoredInAField()
    {
        return VerifyAsync("""
                           using System;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               private Action? _keep;

                               void M()
                               {
                                   Action callback = () => { };
                                   _keep = callback;
                                   IntPtr pointer = Marshal.GetFunctionPointerForDelegate(callback);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsKeptAliveExplicitly()
    {
        return VerifyAsync("""
                           using System;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M()
                               {
                                   Action callback = () => { };
                                   IntPtr pointer = Marshal.GetFunctionPointerForDelegate(callback);
                                   GC.KeepAlive(callback);
                               }
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsAddedToACollection()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               private readonly List<Action> _keep = new();

                               void M()
                               {
                                   Action callback = () => { };
                                   _keep.Add(callback);
                                   IntPtr pointer = Marshal.GetFunctionPointerForDelegate(callback);
                               }
                           }
                           """);
    }
}
