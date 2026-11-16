using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImmutableArrayCreateToArrayAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCreateIsCalledThroughAStaticImport()
    {
        return AnalyzerTestRunner.VerifyAsync<ImmutableArrayCreateToArrayAnalyzer>("""
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using static System.Collections.Immutable.ImmutableArray;
            class C
            {
                void M(List<int> list)
                {
                    var arr = {|E128083:Create(list.ToArray())|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCalledMethodIsNotACreateCall()
    {
        return AnalyzerTestRunner.VerifyAsync<ImmutableArrayCreateToArrayAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Linq;
            class C
            {
                static Func<int[], int> Pick() => values => values.Length;

                void M(List<int> list)
                {
                    var count = Pick()(list.ToArray());
                    var arr = ImmutableArray.Create(list.ToList());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreateMethodBelongsToAnotherType()
    {
        return AnalyzerTestRunner.VerifyAsync<ImmutableArrayCreateToArrayAnalyzer>("""
            using System.Collections.Generic;
            class Factory
            {
                public static int Create(int[] values) => values.Length;
            }
            class C
            {
                int M(List<int> list) => Factory.Create(list.ToArray());
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallsDoNotBind()
    {
        return AnalyzerTestRunner.VerifyAsync<ImmutableArrayCreateToArrayAnalyzer>(
            """
            using System.Collections.Generic;
            using System.Collections.Immutable;
            class C
            {
                void M(List<int> list)
                {
                    var first = Missing.Create(list.ToArray());
                    var second = ImmutableArray.CreateRange<int>(missing.ToArray());
                }
            }
            """,
            expected: null,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFrameworkHasNoImmutableCollectionsMarshal()
    {
        return AnalyzerTestRunner.VerifyAsync<ImmutableArrayCreateToArrayAnalyzer>(
            """
            class C
            {
                void M()
                {
                }
            }
            """,
            expected: null,
            options: new AnalyzerTestOptions { References = ReferenceAssemblies.NetStandard.NetStandard20 });
    }
}
