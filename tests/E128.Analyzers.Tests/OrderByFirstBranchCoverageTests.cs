using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OrderByFirstBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenFirstTakesAPredicate()
    {
        return AnalyzerTestRunner.VerifyAsync<OrderByFirstAnalyzer>("""
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                int M(List<int> items)
                {
                    return items.OrderBy(x => x).First(x => x > 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOrderByTakesAComparerOrIsFollowedByThenBy()
    {
        return AnalyzerTestRunner.VerifyAsync<OrderByFirstAnalyzer>("""
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                int M(List<int> items)
                {
                    var first = items.OrderBy(x => x, Comparer<int>.Default).First();
                    var second = items.OrderBy(x => x).ThenBy(x => x).First();
                    return first + second;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFirstCallFollowsAHelperOrAMethodGroup()
    {
        return AnalyzerTestRunner.VerifyAsync<OrderByFirstAnalyzer>("""
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                static IEnumerable<int> Sorted(List<int> items) => items;

                int M(List<int> items)
                {
                    return Sorted(items).First();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSourceIsAQueryable()
    {
        return AnalyzerTestRunner.VerifyAsync<OrderByFirstAnalyzer>("""
            using System.Linq;
            class C
            {
                int M(IQueryable<int> items)
                {
                    return items.OrderBy(x => x).First();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherNamespaceDefinesTheEnumerableMethods()
    {
        return AnalyzerTestRunner.VerifyAsync<OrderByFirstAnalyzer>("""
            using System.Collections.Generic;

            namespace MyLinq
            {
                public static class Enumerable
                {
                    public static List<T> OrderBy<T>(this List<T> source, System.Func<T, int> key) => source;

                    public static T First<T>(this List<T> source) => source[0];
                }
            }

            namespace Other.Linq
            {
                public static class Enumerable
                {
                    public static List<T> OrderByDescending<T>(this List<T> source, System.Func<T, int> key) => source;

                    public static T FirstOrDefault<T>(this List<T> source) => source[0];
                }
            }

            namespace App
            {
                using MyLinq;
                using Other.Linq;

                class C
                {
                    int M(List<int> items)
                    {
                        return items.OrderBy(x => x).First() + items.OrderByDescending(x => x).FirstOrDefault();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyTheFirstCallComesFromAnotherType()
    {
        return AnalyzerTestRunner.VerifyAsync<OrderByFirstAnalyzer>("""
            using System.Collections.Generic;
            using System.Linq;

            static class Extras
            {
                public static T First<T>(this IOrderedEnumerable<T> source) => default!;
            }

            class C
            {
                int M(List<int> items)
                {
                    return Extras.First(items.OrderBy(x => x)) + items.OrderBy(x => x).First();
                }
            }
            """);
    }
}
