using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ReadonlyFieldLoopHoistCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ReadonlyFieldLoopHoistAnalyzer, ReadonlyFieldLoopHoistCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForLoop_HoistsLocalBeforeTheLoop()
    {
        return VerifyFixAsync(
            """
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
            """,
            """
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    int[] items = _items;
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
    public Task SiblingLoops_EachHoistedByFixAll()
    {
        // The second loop picks the suffixed name, so a fix-all batch from one snapshot cannot
        // emit two locals with the same name.
        return VerifyFixAsync(
            """
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    var index = 0;
                    {|E128235:for (int i = 0; i < _items.Length; i++)
                    {
                        total += _items[i];
                    }|}

                    {|E128235:foreach (var item in _items)
                    {
                        total += _items[index];
                        index++;
                    }|}

                    return total;
                }
            }
            """,
            """
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    var index = 0;
                    int[] items = _items;
                    for (int i = 0; i < items.Length; i++)
                    {
                        total += items[i];
                    }
                    int[] items1 = _items;

                    foreach (var item in items1)
                    {
                        total += items1[index];
                        index++;
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NameCollision_FallsBackToSuffixedName()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum(int[] items)
                {
                    var total = 0;
                    {|E128235:for (int i = 0; i < _items.Length; i++)
                    {
                        total += _items[i] + items[i];
                    }|}

                    return total;
                }
            }
            """,
            """
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum(int[] items)
                {
                    var total = 0;
                    int[] items1 = _items;
                    for (int i = 0; i < items1.Length; i++)
                    {
                        total += items1[i] + items[i];
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThisQualifiedField_RewrittenAsWholeAccess()
    {
        return VerifyFixAsync(
            """
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
            """,
            """
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    int[] items = _items;
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
    public Task CompactLoopBody_StillRewritten()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    {|E128235:for (int i = 0; i < _items.Length; i++)
                        total += _items[i];|}

                    return total;
                }
            }
            """,
            """
            class C
            {
                private readonly int[] _items = new int[10];

                int Sum()
                {
                    var total = 0;
                    int[] items = _items;
                    for (int i = 0; i < items.Length; i++)
                        total += items[i];

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MutableSiblingLoop_KeepsItsFieldReferences()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private readonly int[] _items = new int[10];
                private int[] _buffer = new int[10];

                int Sum()
                {
                    var total = 0;
                    {|E128235:for (int i = 0; i < _items.Length; i++)
                    {
                        total += _items[i];
                    }|}

                    for (int i = 0; i < _buffer.Length; i++)
                    {
                        total += _buffer[i];
                    }

                    return total;
                }
            }
            """,
            """
            class C
            {
                private readonly int[] _items = new int[10];
                private int[] _buffer = new int[10];

                int Sum()
                {
                    var total = 0;
                    int[] items = _items;
                    for (int i = 0; i < items.Length; i++)
                    {
                        total += items[i];
                    }

                    for (int i = 0; i < _buffer.Length; i++)
                    {
                        total += _buffer[i];
                    }

                    return total;
                }
            }
            """);
    }
}
