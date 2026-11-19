using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ArrayReferenceComparisonCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Equality_ReplacedWithSequenceEqual_AndUsingAdded()
    {
        return CodeFixTestRunner.RunAsync<ArrayReferenceComparisonAnalyzer, ArrayReferenceComparisonCodeFixProvider>(
            """
            class C
            {
                bool M(int[] left, int[] right) => {|E128238:left == right|};
            }
            """,
            """
            using System.Linq;

            class C
            {
                bool M(int[] left, int[] right) => Enumerable.SequenceEqual(left, right);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Inequality_ReplacedWithNegatedSequenceEqual()
    {
        return CodeFixTestRunner.RunAsync<ArrayReferenceComparisonAnalyzer, ArrayReferenceComparisonCodeFixProvider>(
            """
            using System.Linq;

            class C
            {
                bool M(int[] left, int[] right) => {|E128238:left != right|};
            }
            """,
            """
            using System.Linq;

            class C
            {
                bool M(int[] left, int[] right) => !Enumerable.SequenceEqual(left, right);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticObjectEquals_ReplacedWithSequenceEqual()
    {
        return CodeFixTestRunner.RunAsync<ArrayReferenceComparisonAnalyzer, ArrayReferenceComparisonCodeFixProvider>(
            """
            using System.Linq;

            class C
            {
                bool M(int[] left, int[] right) => {|E128238:object.Equals(left, right)|};
            }
            """,
            """
            using System.Linq;

            class C
            {
                bool M(int[] left, int[] right) => Enumerable.SequenceEqual(left, right);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InstanceEquals_ReplacedWithSequenceEqual()
    {
        return CodeFixTestRunner.RunAsync<ArrayReferenceComparisonAnalyzer, ArrayReferenceComparisonCodeFixProvider>(
            """
            using System.Linq;

            class C
            {
                bool M(int[] left, int[] right) => {|E128238:left.Equals(right)|};
            }
            """,
            """
            using System.Linq;

            class C
            {
                bool M(int[] left, int[] right) => Enumerable.SequenceEqual(left, right);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TwoComparisons_BothReplacedByFixAll()
    {
        return CodeFixTestRunner.RunAsync<ArrayReferenceComparisonAnalyzer, ArrayReferenceComparisonCodeFixProvider>(
            """
            class C
            {
                bool M(int[] left, int[] right) =>
                    {|E128238:left == right|} || {|E128238:left != right|};
            }
            """,
            """
            using System.Linq;

            class C
            {
                bool M(int[] left, int[] right) =>
                    Enumerable.SequenceEqual(left, right) || !Enumerable.SequenceEqual(left, right);
            }
            """);
    }
}
