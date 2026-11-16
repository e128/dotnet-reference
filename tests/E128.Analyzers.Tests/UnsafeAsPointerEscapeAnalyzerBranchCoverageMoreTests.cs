using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeAsPointerEscapeAnalyzerBranchCoverageMoreTests
{
    private static AnalyzerTestOptions Options => new() { AllowUnsafe = true };

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePointerTargetsAStackLocalInAPlainLambdaOrLocalFunction()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsPointerEscapeAnalyzer>(
            """
            using System;
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M()
                {
                    Action action = () =>
                    {
                        int number = 1;
                        void* pointer = Unsafe.AsPointer(ref number);
                    };

                    void Local()
                    {
                        int number = 2;
                        void* pointer = Unsafe.AsPointer(ref number);
                    }
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStackLocalLivesInAnAsyncLambda()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsPointerEscapeAnalyzer>(
            """
            using System;
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;
            class C
            {
                void M()
                {
                    Func<Task> action = async () =>
                    {
                        int number = 1;
                        unsafe
                        {
                            void* pointer = {|E128125:Unsafe.AsPointer(ref number)|};
                        }

                        await Task.Yield();
                    };
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStackLocalLivesInAnAsyncLocalFunction()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsPointerEscapeAnalyzer>(
            """
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;
            class C
            {
                void M()
                {
                    async Task Local()
                    {
                        int number = 1;
                        unsafe
                        {
                            void* pointer = {|E128125:Unsafe.AsPointer(ref number)|};
                        }

                        await Task.Yield();
                    }
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePointerTargetsAStackLocalInAPropertyAccessor()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsPointerEscapeAnalyzer>(
            """
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                int Value
                {
                    get
                    {
                        int number = 1;
                        void* pointer = Unsafe.AsPointer(ref number);
                        return number;
                    }
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePointerTargetsARefLocalOrARefParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsPointerEscapeAnalyzer>(
            """
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M(ref int parameter)
                {
                    int number = 1;
                    ref int alias = ref number;
                    void* first = {|E128125:Unsafe.AsPointer(ref alias)|};
                    void* second = {|E128125:Unsafe.AsPointer(ref parameter)|};
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFrameworkHasNoUnsafeClass()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsPointerEscapeAnalyzer>(
            """
            class C
            {
                void M()
                {
                }
            }
            """,
            expected: null,
            options: new AnalyzerTestOptions { References = ReferenceAssemblies.NetStandard.NetStandard20 });
    }
}
