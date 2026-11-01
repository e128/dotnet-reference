using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeUnalignedNonBlittableCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<
            UnsafeUnalignedNonBlittableAnalyzer,
            UnsafeUnalignedNonBlittableCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.SolutionTransforms.Add(CodeFixTestRunner.AllowUnsafe);

        return test.RunAsync();
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
