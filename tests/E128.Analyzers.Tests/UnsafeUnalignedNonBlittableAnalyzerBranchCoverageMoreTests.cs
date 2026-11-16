using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeUnalignedNonBlittableAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeUnalignedNonBlittableAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInvokedExpressionHasNoSimpleName()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                int M(Func<Func<int>> factory)
                {
                    return factory()();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTargetIsATypeParameter()
    {
        return VerifyAsync("""
            using System.Runtime.CompilerServices;
            class C
            {
                T M<T>(ref byte source)
                {
                    return Unsafe.ReadUnaligned<T>(ref source);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherTypeDeclaresReadUnaligned()
    {
        return VerifyAsync("""
            class Own
            {
                public static T ReadUnaligned<T>(int offset) => default!;
            }

            class C
            {
                bool M()
                {
                    return Own.ReadUnaligned<bool>(0);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsDynamic()
    {
        return VerifyAsync("""
            class C
            {
                object M(dynamic reader)
                {
                    return reader.ReadUnaligned<bool>(0);
                }
            }
            """);
    }
}
