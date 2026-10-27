using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SequenceEqualLoopAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<SequenceEqualLoopAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLoopReturnsFalseOnMismatch()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNoLengthGuardPrecedesTheLoop()
    {
        return VerifyAsync("""
                           class C
                           {
                               bool M(int[] left, int[] right)
                               {
                                   for (int i = 0; i < left.Length; i++)
                                   {
                                       if (left[i] != right[i])
                                       {
                                           return false;
                                       }
                                   }
                                   return true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenElementsAreFloatingPoint()
    {
        return VerifyAsync("""
                           class C
                           {
                               bool M(double[] left, double[] right)
                               {
                                   if (left.Length != right.Length)
                                   {
                                       return false;
                                   }

                                   for (int i = 0; i < left.Length; i++)
                                   {
                                       if (left[i] != right[i])
                                       {
                                           return false;
                                       }
                                   }
                                   return true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBoundComesFromAnotherArray()
    {
        return VerifyAsync("""
                           class C
                           {
                               bool M(int[] left, int[] right, int[] other)
                               {
                                   if (left.Length != right.Length)
                                   {
                                       return false;
                                   }

                                   for (int i = 0; i < other.Length; i++)
                                   {
                                       if (left[i] != right[i])
                                       {
                                           return false;
                                       }
                                   }
                                   return true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopDoesMoreThanCompare()
    {
        return VerifyAsync("""
                           class C
                           {
                               bool M(int[] left, int[] right)
                               {
                                   for (int i = 0; i < left.Length; i++)
                                   {
                                       if (left[i] != right[i])
                                       {
                                           System.Console.WriteLine(i);
                                           return false;
                                       }
                                   }
                                   return true;
                               }
                           }
                           """);
    }
}
