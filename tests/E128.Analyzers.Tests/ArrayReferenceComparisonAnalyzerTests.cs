using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ArrayReferenceComparisonAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForEqualityOperatorOnTwoArrays()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(int[] left, int[] right) => {|E128238:left == right|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForInequalityOperatorOnTwoArrays()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(int[] left, int[] right) => {|E128238:left != right|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForStaticObjectEquals()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(int[] left, int[] right) => {|E128238:object.Equals(left, right)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForInstanceEqualsOnAnArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(int[] left, int[] right) => {|E128238:left.Equals(right)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForStringArrayComparison()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(string[] left, string[] right) => {|E128238:left == right|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForNullCheck()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(int[] values) => values == null || values != null;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForDefaultValueCheck()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(int[] values) => values == default;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForByteArrayComparison()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(byte[] left, byte[] right) => left == right;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForMultiDimensionalArrayComparison()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(int[,] left, int[,] right) => left == right;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForComparisonAgainstANonArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(int[] left, object right) => left == right;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserTypeWithAnOverriddenEquals()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class Box
            {
                public override bool Equals(object? other) => true;
            }

            class C
            {
                bool M(Box left, Box right) => left.Equals(right);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnExplicitReferenceEqualsCall()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayReferenceComparisonAnalyzer>("""
            class C
            {
                bool M(int[] left, int[] right) => object.ReferenceEquals(left, right);
            }
            """);
    }
}
