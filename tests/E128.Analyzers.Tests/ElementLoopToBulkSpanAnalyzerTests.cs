using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ElementLoopToBulkSpanAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsAProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>("""
                           class C
                           {
                               int[] Values { get; } = new int[4];

                               void M()
                               {
                                   for (var i = 0; i < Values.Length; i++)
                                   {
                                       Values[i] = 1;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverContainsACall()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>("""
                           class C
                           {
                               int[] GetBuffer()
                               {
                                   return new int[4];
                               }

                               void M()
                               {
                                   for (var i = 0; i < GetBuffer().Length; i++)
                                   {
                                       GetBuffer()[i] = 1;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenFillingAnArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>("""
                           class C
                           {
                               void M(int[] values)
                               {
                                   {|E128140:for (var i = 0; i < values.Length; i++)
                                   {
                                       values[i] = 1;
                                   }|}
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenClearingASpan()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(Span<byte> buffer)
                               {
                                   {|E128140:for (var i = 0; i < buffer.Length; i++)
                                   {
                                       buffer[i] = 0;
                                   }|}
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCopyingAnArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>("""
                           class C
                           {
                               void M(byte[] source, byte[] destination)
                               {
                                   {|E128140:for (var i = 0; i < source.Length; i++)
                                   {
                                       destination[i] = source[i];
                                   }|}
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueReadsTheLoopVariable()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>("""
                           class C
                           {
                               void M(int[] values)
                               {
                                   for (var i = 0; i < values.Length; i++)
                                   {
                                       values[i] = i;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFillValueIsAMethodCall()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>("""
                           class C
                           {
                               int Next()
                               {
                                   return 1;
                               }

                               void M(int[] values)
                               {
                                   for (var i = 0; i < values.Length; i++)
                                   {
                                       values[i] = Next();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBoundBelongsToAnotherCollection()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>("""
                           class C
                           {
                               void M(int[] values, int[] bounds)
                               {
                                   for (var i = 0; i < bounds.Length; i++)
                                   {
                                       values[i] = 1;
                                   }
                               }
                           }
                           """);
    }
}
