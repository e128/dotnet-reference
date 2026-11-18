using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ReadonlyFieldLoopHoistAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForLoopReadingReadonlyArrayField()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    {|E128235:for (int i = 0; i < _items.Length; i++)
                    {
                        total += _items[i];
                    }|}

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForEachLoopReadingReadonlyArrayFieldInBody()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    var index = 0;
                    {|E128235:foreach (var item in _items)
                    {
                        total += _items[index];
                        index++;
                    }|}

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhileLoopReadingReadonlyArrayField()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    var i = 0;
                    {|E128235:while (i < _items.Length)
                    {
                        total += _items[i];
                        i++;
                    }|}

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_DoLoopReadingReadonlyArrayField()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    var i = 0;
                    {|E128235:do
                    {
                        total += _items[i];
                        i++;
                    }
                    while (i < _items.Length);|}

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFieldIsReadThroughThisQualifier()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    {|E128235:for (int i = 0; i < this._items.Length; i++)
                    {
                        total += this._items[i];
                    }|}

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheReadSitsInsideALambdaCapturedByTheLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            using System;

            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    {|E128235:for (int i = 0; i < _items.Length; i++)
                    {
                        Action add = () => total += _items[i];
                        add();
                    }|}

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StaticReadonlyArrayFieldInLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private static readonly int[] _primes = new[] { 2, 3, 5, 7 };

                int Sum()
                {
                    var total = 0;
                    {|E128235:for (int i = 0; i < _primes.Length; i++)
                    {
                        total += _primes[i];
                    }|}

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnceForTheOutermostLoopWhenLoopsNest()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    {|E128235:for (int i = 0; i < _items.Length; i++)
                    {
                        for (int j = 0; j < _items.Length; j++)
                        {
                            total += _items[i] * _items[j];
                        }
                    }|}

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_MultiDimensionalArrayFieldInLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[,] _grid = new int[3, 3];

                int Sum()
                {
                    var total = 0;
                    {|E128235:for (int i = 0; i < _grid.GetLength(0); i++)
                    {
                        total += _grid[i, i];
                    }|}

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForMutableArrayField()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    for (int i = 0; i < _items.Length; i++)
                    {
                        total += _items[i];
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForReadonlyNonArrayFieldReadByIndex()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly System.Collections.Generic.List<int> _items = new();

                int Sum()
                {
                    var total = 0;
                    for (int i = 0; i < _items.Count; i++)
                    {
                        total += _items[i];
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFieldIsReadOutsideAnyLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    return _items.Length + _items[0];
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopReadsALocalArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                int Sum()
                {
                    var items = new int[10];
                    var total = 0;
                    for (int i = 0; i < items.Length; i++)
                    {
                        total += items[i];
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyTheForEachSourceReadsTheField()
    {
        // The foreach source runs once, so its read does not repeat per iteration.
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    foreach (var item in _items)
                    {
                        total += item;
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConstructorLoopAssignsTheField()
    {
        // A constructor can assign a readonly field, so a hoisted copy would hide the new value.
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _items = new int[10];

                public C()
                {
                    for (int i = 0; i < _items.Length; i++)
                    {
                        _items = new int[i + 1];
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFieldNameYieldsNoLocalName()
    {
        // The field name `_` strips to an empty remainder, so the fix has no name to declare.
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _ = new int[10];

                int Sum()
                {
                    var total = 0;
                    for (int i = 0; i < _.Length; i++)
                    {
                        total += _[i];
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFieldIsDeclaredOnAnotherType()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class Base
            {
                protected readonly int[] _items = new int[10];
            }

            class C : Base
            {
                int Sum()
                {
                    var total = 0;
                    for (int i = 0; i < _items.Length; i++)
                    {
                        total += _items[i];
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopIsAnEmbeddedStatementWithNoBlock()
    {
        // The fix inserts the local into the parent block. An embedded statement owns no block.
        return AnalyzerTestRunner.VerifyAsync<ReadonlyFieldLoopHoistAnalyzer>("""
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    if (_items.Length > 0)
                        for (int i = 0; i < _items.Length; i++)
                            total += _items[i];

                    return total;
                }
            }
            """);
    }
}
