using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeReadWriteAlignmentCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<UnsafeReadWriteAlignmentAnalyzer, UnsafeReadWriteAlignmentCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions { AllowUnsafe = true });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RenamesReadToReadUnaligned()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                int M(void* pointer)
                {
                    return {|E128113:Unsafe.Read<int>(pointer)|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                int M(void* pointer)
                {
                    return Unsafe.ReadUnaligned<int>(pointer);
                }
            }
            """);
    }
}
