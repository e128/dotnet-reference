using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ManualBitCountAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLoopCountsTheLowBit()
    {
        return AnalyzerTestRunner.VerifyAsync<ManualBitCountAnalyzer>("""
                           class C
                           {
                               int Count(int value)
                               {
                                   int count = 0;
                                   {|E128134:while (value != 0)
                                   {
                                       count += value & 1;
                                       value >>= 1;
                                   }|}
                                   return count;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheConditionIsReversed()
    {
        return AnalyzerTestRunner.VerifyAsync<ManualBitCountAnalyzer>("""
                           class C
                           {
                               long Count(long value)
                               {
                                   long count = 0;
                                   {|E128134:while (0 != value)
                                   {
                                       count += value & 1;
                                       value >>= 1;
                                   }|}
                                   return count;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueIsReadAfterTheLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ManualBitCountAnalyzer>("""
                           class C
                           {
                               int Count(int value)
                               {
                                   int count = 0;
                                   while (value != 0)
                                   {
                                       count += value & 1;
                                       value >>= 1;
                                   }
                                   return count + value;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopBodyHasAThirdStatement()
    {
        return AnalyzerTestRunner.VerifyAsync<ManualBitCountAnalyzer>("""
                           class C
                           {
                               int Count(int value)
                               {
                                   int count = 0;
                                   while (value != 0)
                                   {
                                       count += value & 1;
                                       value >>= 1;
                                       count++;
                                   }
                                   return count;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConditionIsNotAnInequality()
    {
        return AnalyzerTestRunner.VerifyAsync<ManualBitCountAnalyzer>("""
                           class C
                           {
                               int Count(int value)
                               {
                                   int count = 0;
                                   while (value > 0)
                                   {
                                       count += value & 1;
                                       value >>= 1;
                                   }
                                   return count;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCountIsUnsigned()
    {
        return AnalyzerTestRunner.VerifyAsync<ManualBitCountAnalyzer>("""
                           class C
                           {
                               uint Count(uint value)
                               {
                                   uint count = 0;
                                   while (value != 0)
                                   {
                                       count += value & 1;
                                       value >>= 1;
                                   }
                                   return count;
                               }
                           }
                           """);
    }
}
