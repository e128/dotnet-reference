using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeUnalignedNonBlittableCodeFixProviderBranchCoverageMoreTests
{
    private static Task NoFixAsync(string source)
    {
        return CodeFixTestRunner.RunAsync<UnsafeUnalignedNonBlittableAnalyzer, UnsafeUnalignedNonBlittableCodeFixProvider>(
            source,
            source,
            options: new CodeFixTestOptions { AllowUnsafe = true });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCallWritesInsteadOfReads()
    {
        return NoFixAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M(ref byte destination)
                {
                    {|E128139:Unsafe.WriteUnaligned<bool>(ref destination, true)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheTypeArgumentIsInferred()
    {
        return NoFixAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M(ref byte destination)
                {
                    {|E128139:Unsafe.WriteUnaligned(ref destination, true)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheTypeArgumentIsAnotherNonBlittableType()
    {
        return NoFixAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                string M(ref byte source)
                {
                    return {|E128139:Unsafe.ReadUnaligned<string>(ref source)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheTypeArgumentIsNotWrittenAsTheBoolKeyword()
    {
        return NoFixAsync("""
            using System;
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                bool M(ref byte source)
                {
                    return {|E128139:Unsafe.ReadUnaligned<Boolean>(ref source)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheReadIsAStatementByItself()
    {
        return NoFixAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M(ref byte source)
                {
                    {|E128139:Unsafe.ReadUnaligned<bool>(ref source)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheArgumentIsAPointer()
    {
        return NoFixAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                bool M(byte* source)
                {
                    return {|E128139:Unsafe.ReadUnaligned<bool>(source)|};
                }
            }
            """);
    }
}
