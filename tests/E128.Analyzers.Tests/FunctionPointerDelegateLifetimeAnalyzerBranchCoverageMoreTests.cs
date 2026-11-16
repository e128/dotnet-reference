using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FunctionPointerDelegateLifetimeAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FunctionPointerDelegateLifetimeAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsStoredInAnArrayElement()
    {
        return VerifyAsync("""
            using System;
            using System.Runtime.InteropServices;
            class C
            {
                void M()
                {
                    var keep = new Action[1];
                    Action callback = () => { };
                    keep[0] = callback;
                    IntPtr pointer = Marshal.GetFunctionPointerForDelegate(callback);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherTypeDeclaresAMethodWithTheSameName()
    {
        return VerifyAsync("""
            using System;
            class Own
            {
                public static IntPtr GetFunctionPointerForDelegate(Action callback) => IntPtr.Zero;
            }

            class C
            {
                void M()
                {
                    Action callback = () => { };
                    IntPtr pointer = Own.GetFunctionPointerForDelegate(callback);
                }
            }
            """);
    }


    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsStoredInAProperty()
    {
        return VerifyAsync("""
            using System;
            using System.Runtime.InteropServices;
            class C
            {
                Action? Keep { get; set; }

                void M()
                {
                    Action callback = () => { };
                    Keep = callback;
                    IntPtr pointer = Marshal.GetFunctionPointerForDelegate(callback);
                }
            }
            """);
    }
}
