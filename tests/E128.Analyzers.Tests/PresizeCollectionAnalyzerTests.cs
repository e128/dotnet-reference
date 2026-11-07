using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PresizeCollectionAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task List_FilledInForeach_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                List<int> M(IReadOnlyList<int> source)
                {
                    List<int> result = {|E128205:new()|};
                    foreach (int item in source)
                    {
                        result.Add(item);
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Dictionary_FilledInFor_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                Dictionary<string, int> M(IReadOnlyList<string> keys)
                {
                    Dictionary<string, int> map = {|E128205:new()|};
                    for (int i = 0; i < keys.Count; i++)
                    {
                        map.Add(keys[i], i);
                    }

                    return map;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ArraySource_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                List<int> M(int[] source)
                {
                    List<int> result = {|E128205:new()|};
                    foreach (int item in source)
                    {
                        result.Add(item);
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AlreadySized_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                List<int> M(IReadOnlyList<int> source)
                {
                    List<int> result = new(source.Count);
                    foreach (int item in source)
                    {
                        result.Add(item);
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoAddInLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                List<int> M(IReadOnlyList<int> source)
                {
                    List<int> result = new();
                    foreach (int item in source)
                    {
                        if (item > 0)
                        {
                            continue;
                        }
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralBound_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                List<int> M()
                {
                    List<int> result = new();
                    for (int i = 0; i < 10; i++)
                    {
                        result.Add(i);
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SortedSet_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                SortedSet<int> M(IReadOnlyList<int> source)
                {
                    SortedSet<int> result = new();
                    foreach (int item in source)
                    {
                        result.Add(item);
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Queue_FilledWithEnqueue_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                Queue<int> M(IReadOnlyList<int> source)
                {
                    Queue<int> result = {|E128205:new()|};
                    foreach (int item in source)
                    {
                        result.Enqueue(item);
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Stack_FilledWithPush_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                Stack<int> M(IReadOnlyList<int> source)
                {
                    Stack<int> result = {|E128205:new()|};
                    foreach (int item in source)
                    {
                        result.Push(item);
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CapacitySetByInitializer_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                List<int> M(IReadOnlyList<int> source)
                {
                    List<int> result = new() { Capacity = 32 };
                    foreach (int item in source)
                    {
                        result.Add(item);
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SwitchBreak_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                List<int> M(IReadOnlyList<int> source)
                {
                    List<int> result = {|E128205:new()|};
                    foreach (int item in source)
                    {
                        switch (item)
                        {
                            case 0:
                                break;
                        }

                        result.Add(item);
                    }

                    return result;
                }
            }
            """);
    }
}
