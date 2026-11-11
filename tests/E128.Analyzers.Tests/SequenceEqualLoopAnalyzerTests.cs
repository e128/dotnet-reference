using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SequenceEqualLoopAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLoopReturnsFalseOnMismatch()
    {
        return AnalyzerTestRunner.VerifyAsync<SequenceEqualLoopAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SequenceEqualLoopAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SequenceEqualLoopAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SequenceEqualLoopAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SequenceEqualLoopAnalyzer>("""
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
