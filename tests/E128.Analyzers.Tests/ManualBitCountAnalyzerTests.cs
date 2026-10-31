using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ManualBitCountAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<ManualBitCountAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLoopCountsTheLowBit()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
