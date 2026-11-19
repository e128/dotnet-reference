using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ArrayGrowthInLoopAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForResizeInsideAForLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayGrowthInLoopAnalyzer>("""
            class C
            {
                void M(ref byte[] buffer)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        {|E128249:System.Array.Resize(ref buffer, buffer.Length + 1)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForResizeInsideAWhileLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayGrowthInLoopAnalyzer>("""
            class C
            {
                void M(ref byte[] buffer)
                {
                    while (buffer.Length < 10)
                    {
                        {|E128249:System.Array.Resize(ref buffer, buffer.Length + 1)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForConcatAssignedToTheSameArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayGrowthInLoopAnalyzer>("""
            using System.Linq;

            class C
            {
                void M(byte[] buffer, byte[] extra)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        buffer = {|E128249:buffer.Concat(extra)|}.ToArray();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForStaticConcatAssignedToTheSameArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayGrowthInLoopAnalyzer>("""
            using System.Linq;

            class C
            {
                void M(byte[] buffer, byte[] extra)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        buffer = {|E128249:Enumerable.Concat(buffer, extra)|}.ToArray();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenResizeRunsOutsideALoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayGrowthInLoopAnalyzer>("""
            class C
            {
                void M(ref byte[] buffer)
                {
                    System.Array.Resize(ref buffer, buffer.Length + 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenConcatIsAssignedToAnotherArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayGrowthInLoopAnalyzer>("""
            using System.Linq;

            class C
            {
                void M(byte[] buffer, byte[] extra)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        var result = buffer.Concat(extra).ToArray();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserTypeConcat()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayGrowthInLoopAnalyzer>("""
            class Bag
            {
                public Bag Concat(Bag other) => this;
            }

            class C
            {
                void M(Bag bag, Bag other)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        bag = bag.Concat(other);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenConcatSitsInsideALambda()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayGrowthInLoopAnalyzer>("""
            using System;
            using System.Linq;

            class C
            {
                void M(byte[] buffer, byte[] extra)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        Action resize = () => buffer = buffer.Concat(extra).ToArray();
                        resize();
                    }
                }
            }
            """);
    }
}
