using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ListGetRangeIndirectMutationTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyGrowsTheListThroughAHelper()
    {
        return CodeFixTestRunner.NoFixAsync<ListGetRangeAnalyzer, ListGetRangeCodeFixProvider>(
            """
            using System.Collections.Generic;

            class C
            {
                private readonly List<string> _items = new List<string> { "a", "" };

                int Total()
                {
                    int total = 0;
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (_items[i].Length == 0)
                        {
                            Grow();
                        }

                        total += _items[i].Length;
                    }

                    return total;
                }

                void Grow()
                {
                    _items.Add("added");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyCallsADelegate()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>(
            """
            using System;
            using System.Collections.Generic;

            class C
            {
                int Total(List<string> items, Action<int> callback)
                {
                    int total = 0;
                    for (int i = 0; i < items.Count; i++)
                    {
                        callback(i);
                        total += items[i].Length;
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyCallsALocalFunction()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>(
            """
            using System.Collections.Generic;

            class C
            {
                int Total(List<string> items)
                {
                    int total = 0;
                    void Touch()
                    {
                        items.Add("more");
                    }

                    for (int i = 0; i < items.Count; i++)
                    {
                        Touch();
                        total += items[i].Length;
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyCallsAnOverridableMemberOnAnElement()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>(
            """
            using System.Collections.Generic;

            class Node
            {
            }

            class C
            {
                int Total(List<Node> items)
                {
                    int total = 0;
                    for (int i = 0; i < items.Count; i++)
                    {
                        total += items[i].ToString().Length;
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyReadsAPropertyWithAnAccessorBody()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>(
            """
            using System.Collections.Generic;

            class Node
            {
                public int Size => 1;
            }

            class C
            {
                int Total(List<Node> items)
                {
                    int total = 0;
                    for (int i = 0; i < items.Count; i++)
                    {
                        total += items[i].Size;
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyAwaits()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            class C
            {
                async Task<int> Total(List<string> items)
                {
                    int total = 0;
                    for (int i = 0; i < items.Count; i++)
                    {
                        await Task.Yield();
                        total += items[i].Length;
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBodyOnlyUsesFrameworkMembersAndAutoProperties()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>(
            """
            using System;
            using System.Collections.Generic;

            class Node
            {
                public int Size { get; set; }
            }

            class C
            {
                int Total(List<Node> items, List<string> names)
                {
                    int total = 0;
                    {|#0:for (int i = 0; i < items.Count; i++)
                    {
                        total += items[i].Size + Math.Abs(names.Count) + "x".Trim().Length;
                        Console.WriteLine(total);
                    }|}
                    return total;
                }
            }
            """,
            new DiagnosticResult("E128084", DiagnosticSeverity.Warning).WithLocation(0));
    }
}
