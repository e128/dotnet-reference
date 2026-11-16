using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LowValueTestAnalyzerBranchCoverageTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>(
            code,
            options: new AnalyzerTestOptions { References = Net100WithXunit });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAMethodThatIsNotATest()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class Subject
            {
                public void Helper()
                {
                    var left = 1;
                    var right = 2;
                    Assert.Equal(3, left + right);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_TwoExpressionBodiedTestsThatShareTheSameCall()
    {
        return VerifyAsync("""
            using Xunit;

            public static class Calculator
            {
                public static int Add(int left, int right) => left + right;
            }

            public sealed class Subject
            {
                [Fact]
                public void {|E128107:Should_ProducePositiveValue|}() => Assert.NotEqual(0, Calculator.Add(1, 2));

                [Fact]
                public void {|E128107:Should_ProduceNonZeroValue|}() => Assert.NotEqual(0, Calculator.Add(1, 2));
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForTestsThatShareOnlyAnInterfaceCall()
    {
        return VerifyAsync("""
            using Xunit;

            public interface IReader
            {
                int Read(int value);
            }

            public sealed class Reader : IReader
            {
                public int Read(int value) => value;
            }

            public sealed class Subject
            {
                [Fact]
                public void Should_ReadTheFirstValue()
                {
                    IReader reader = new Reader();
                    var value = reader.Read(1);
                    Assert.NotEqual(0, value);
                }

                [Fact]
                public void Should_ReadTheSameValueAgain()
                {
                    IReader reader = new Reader();
                    var value = reader.Read(1);
                    Assert.NotEqual(0, value);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAShortTestThatReadsAProductionProperty()
    {
        return VerifyAsync("""
            using Xunit;

            public static class Pool
            {
                public static int Shared => 3;
            }

            public sealed class Subject
            {
                [Fact]
                public void Should_ExposeTheSharedValue()
                {
                    var left = 1;
                    var right = 2;
                    Assert.Equal(left + right, Pool.Shared);
                }
            }
            """);
    }

}
