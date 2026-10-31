using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SequenceEqualLoopCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<
            SequenceEqualLoopAnalyzer,
            SequenceEqualLoopCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheLoopWithSequenceEqual()
    {
        return VerifyFixAsync(
            """
            class C
            {
                bool M(int[] left, int[] right)
                {
                    if (left.Length != right.Length)
                    {
                        return false;
                    }

                    {|E128121:for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] != right[i])
                        {
                            return false;
                        }
                    }|}
                    return true;
                }
            }
            """,
            """
            using System;

            class C
            {
                bool M(int[] left, int[] right)
                {
                    if (left.Length != right.Length)
                    {
                        return false;
                    }

                    return left.AsSpan().SequenceEqual(right);
                }
            }
            """);
    }
}
