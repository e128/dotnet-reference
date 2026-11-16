using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ClosedHierarchyBranchCoverageMoreTests
{
    private static AnalyzerTestOptions Options => new()
    {
        LanguageVersion = LanguageVersion.Latest,
        Sources = [UnionTestRuntime.StubSource],
    };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBaseCarriesAnUnrelatedAttribute()
    {
        return AnalyzerTestRunner.VerifyAsync<AbstractBaseWithMembersAnalyzer>(
            """
            [System.Obsolete]
            internal abstract class {|E128225:Shape|}
            {
                internal string Label() => "shape";
            }

            internal sealed class Circle : Shape
            {
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenASwitchOverAnEnumEndsInADiscardArm()
    {
        return AnalyzerTestRunner.VerifyAsync<UnionSwitchDiscardArmAnalyzer>(
            """
            internal enum Kind
            {
                A,
                B
            }

            internal static class Switcher
            {
                internal static int Switch(Kind kind)
                {
                    return kind switch
                    {
                        Kind.A => 1,
                        _ => 0
                    };
                }
            }
            """,
            options: Options);
    }
}
