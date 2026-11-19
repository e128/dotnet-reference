using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ListSpanMutationAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAddRunsBeforeTheSpanRead()
    {
        return AnalyzerTestRunner.VerifyAsync<ListSpanMutationAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M(List<int> list)
                {
                    var span = CollectionsMarshal.AsSpan(list);
                    {|E128243:list.Add(1)|};
                    Console.WriteLine(span[0]);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForClear()
    {
        return AnalyzerTestRunner.VerifyAsync<ListSpanMutationAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M(List<int> list)
                {
                    var span = CollectionsMarshal.AsSpan(list);
                    {|E128243:list.Clear()|};
                    Console.WriteLine(span[0]);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForSetCount()
    {
        return AnalyzerTestRunner.VerifyAsync<ListSpanMutationAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M(List<int> list)
                {
                    var span = CollectionsMarshal.AsSpan(list);
                    {|E128243:CollectionsMarshal.SetCount(list, 0)|};
                    Console.WriteLine(span[0]);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSpanIsPassedToAMethodAfterTheMutation()
    {
        return AnalyzerTestRunner.VerifyAsync<ListSpanMutationAnalyzer>("""
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M(List<int> list)
                {
                    var span = CollectionsMarshal.AsSpan(list);
                    {|E128243:list.Add(1)|};
                    Use(span);
                }

                static void Use(System.Span<int> span)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSpanIsReadBeforeTheMutationOnly()
    {
        return AnalyzerTestRunner.VerifyAsync<ListSpanMutationAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M(List<int> list)
                {
                    var span = CollectionsMarshal.AsSpan(list);
                    Console.WriteLine(span[0]);
                    list.Add(1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSpanIsNotReadAfterTheMutation()
    {
        return AnalyzerTestRunner.VerifyAsync<ListSpanMutationAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M(List<int> list)
                {
                    var span = CollectionsMarshal.AsSpan(list);
                    list.Add(1);
                    Console.WriteLine("done");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherListIsMutated()
    {
        return AnalyzerTestRunner.VerifyAsync<ListSpanMutationAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M(List<int> list, List<int> other)
                {
                    var span = CollectionsMarshal.AsSpan(list);
                    other.Add(1);
                    Console.WriteLine(span[0]);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnIndexAssignment()
    {
        return AnalyzerTestRunner.VerifyAsync<ListSpanMutationAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M(List<int> list)
                {
                    var span = CollectionsMarshal.AsSpan(list);
                    list[0] = 5;
                    Console.WriteLine(span[0]);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSpanIsUsedImmediately()
    {
        return AnalyzerTestRunner.VerifyAsync<ListSpanMutationAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M(List<int> list)
                {
                    Console.WriteLine(CollectionsMarshal.AsSpan(list).Length);
                    list.Add(1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserTypeWithAnAddMember()
    {
        return AnalyzerTestRunner.VerifyAsync<ListSpanMutationAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class Bag
            {
                public void Add(int value)
                {
                }
            }

            class C
            {
                void M(List<int> list, Bag bag)
                {
                    var span = CollectionsMarshal.AsSpan(list);
                    bag.Add(1);
                    Console.WriteLine(span[0]);
                }
            }
            """);
    }
}
