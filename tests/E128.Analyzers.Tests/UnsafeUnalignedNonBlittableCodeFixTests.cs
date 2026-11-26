using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeUnalignedNonBlittableCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<UnsafeUnalignedNonBlittableAnalyzer, UnsafeUnalignedNonBlittableCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions { AllowUnsafe = true });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ComparesTheByteWithZero()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                bool M(ref byte source)
                {
                    return {|E128139:Unsafe.ReadUnaligned<bool>(ref source)|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                bool M(ref byte source)
                {
                    return (source != 0);
                }
            }
            """);
    }
}
