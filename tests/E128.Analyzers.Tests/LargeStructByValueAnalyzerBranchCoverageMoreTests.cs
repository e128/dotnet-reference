using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LargeStructByValueAnalyzerBranchCoverageMoreTests
{
    private static AnalyzerTestOptions Lenient => new() { CompilerDiagnostics = CompilerDiagnostics.None };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheParameterCarriesAnUnrelatedModifier()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M({|E128217:scoped Big value|})
                {
                }
            }
            """,
            options: Lenient);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheRuntimeLacksInlineArrayAttribute()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M({|E128217:Big value|})
                {
                }
            }
            """,
            options: new AnalyzerTestOptions { References = ReferenceAssemblies.NetStandard.NetStandard20 });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnInlineArrayStructHasNoElementField()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            [System.Runtime.CompilerServices.InlineArray(4)]
            struct Empty
            {
            }

            struct Holder
            {
                public Empty Tail;
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M({|E128217:Holder value|})
                {
                }
            }
            """,
            options: Lenient);
    }
}
