using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeReadWriteAlignmentCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<
            UnsafeReadWriteAlignmentAnalyzer,
            UnsafeReadWriteAlignmentCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.SolutionTransforms.Add(CodeFixTestRunner.AllowUnsafe);

        return test.RunAsync();
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
