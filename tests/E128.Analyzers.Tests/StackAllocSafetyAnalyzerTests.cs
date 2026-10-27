using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StackAllocSafetyAnalyzerTests
{
    private static Task VerifyAsync(string code, bool allowUnsafe = false)
    {
        var test = new CSharpAnalyzerTest<StackAllocSafetyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        if (allowUnsafe)
        {
            test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectCompilationOptions(
                projectId,
                ((CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true)));
        }

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenInsideLoop()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int count)
                               {
                                   for (var i = 0; i < count; i++)
                                   {
                                       Span<byte> buffer = {|E128109:stackalloc byte[16]|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenConstantSizeExceedsLimit()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   Span<int> buffer = {|E128109:stackalloc int[300]|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenLengthIsUnbounded()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length)
                               {
                                   Span<byte> buffer = {|E128109:stackalloc byte[length]|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenSmallConstantSize()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   Span<byte> buffer = stackalloc byte[256];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenGuardedByConditional()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length)
                               {
                                   Span<byte> buffer = length <= 256 ? stackalloc byte[length] : new byte[length];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenLengthUsesMathMin()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length)
                               {
                                   Span<byte> buffer = stackalloc byte[Math.Min(length, 256)];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenLoopIsOutsideLocalFunction()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int count)
                               {
                                   for (var i = 0; i < count; i++)
                                   {
                                       Use(i);
                                   }

                                   void Use(int n)
                                   {
                                       Span<byte> buffer = stackalloc byte[16];
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenGuardIgnoresLength()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length, bool flag)
                               {
                                   if (flag)
                                   {
                                       Span<byte> buffer = {|E128109:stackalloc byte[length]|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAssignedToPointer()
    {
        return VerifyAsync(
            """
            class C
            {
                unsafe void M()
                {
                    byte* buffer = {|E128109:stackalloc byte[16]|};
                }
            }
            """,
            allowUnsafe: true);
    }
}
