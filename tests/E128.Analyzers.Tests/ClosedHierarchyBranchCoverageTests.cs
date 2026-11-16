using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ClosedHierarchyBranchCoverageTests
{
    private static AnalyzerTestOptions Options => new()
    {
        LanguageVersion = LanguageVersion.Latest,
        Sources = [UnionTestRuntime.StubSource],
    };

    private static Task VerifyMembersAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<AbstractBaseWithMembersAnalyzer>(code, options: Options);
    }

    private static Task VerifyMemberlessAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<MemberlessAbstractBaseAnalyzer>(code, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBaseCarriesAnAttributeWithTheSameNameInAnotherNamespace()
    {
        return VerifyMembersAsync(
            """
            namespace Other
            {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                internal sealed class IsClosedTypeAttribute : System.Attribute
                {
                }
            }

            [Other.IsClosedType]
            internal abstract class {|E128225:Shape|}
            {
                internal string Label() => "shape";
            }

            internal sealed class Circle : Shape
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenEveryPartOfAPartialBaseDeclaresMembers()
    {
        return VerifyMembersAsync(
            """
            internal abstract partial class {|E128225:Shape|}
            {
                internal string Label() => "shape";
            }

            internal abstract partial class Shape
            {
                internal int Sides() => 0;
            }

            internal sealed class Circle : Shape
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAGenericBaseWithAClosedDerivedType()
    {
        return VerifyMembersAsync(
            """
            internal abstract class {|E128225:Shape|}<T>
            {
                internal T Value() => default!;
            }

            internal sealed class Circle : Shape<int>
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForBasesThatAreNotInternalAbstractClasses()
    {
        return VerifyMembersAsync(
            """
            public abstract class PublicShape
            {
                internal string Label() => "shape";
            }

            internal class OpenShape
            {
                internal string Label() => "shape";
            }

            internal interface IShape
            {
                string Label();
            }

            internal abstract record RecordShape
            {
                internal string Label() => "shape";
            }

            internal sealed class A : PublicShape
            {
            }

            internal sealed class B : OpenShape
            {
            }

            internal sealed class C : IShape
            {
                public string Label() => "c";
            }

            internal sealed record D : RecordShape;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnAbstractBaseWithoutDerivedTypes()
    {
        return VerifyMemberlessAsync(
            """
            internal abstract class Shape
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAMemberlessRecordWithoutAParameterList()
    {
        return VerifyMemberlessAsync(
            """
            internal abstract record {|E128219:Shape|};

            internal sealed record Circle : Shape;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAMemberlessRecordWithAParameterList()
    {
        return VerifyMemberlessAsync(
            """
            internal abstract record Shape(int Id);

            internal sealed record Circle(int Id) : Shape(Id);
            """);
    }
}
