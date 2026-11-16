using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MemberlessAbstractBaseAnalyzerTests
{
    private static Task VerifyAsync(
        string code,
        DiagnosticResult[]? expected = null,
        LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        return AnalyzerTestRunner.VerifyAsync<MemberlessAbstractBaseAnalyzer>(
            code,
            expected,
            new AnalyzerTestOptions
            {
                LanguageVersion = languageVersion,
                Sources = [UnionTestRuntime.StubSource],
                SuppressedDiagnosticIds = ["RSEXPERIMENTAL006"],
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAMemberlessInternalAbstractBaseHasOneDerivedType()
    {
        return VerifyAsync(
            """
            internal abstract class {|E128219:Shape|}
            {
            }

            internal sealed class Circle : Shape
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenDerivationIsTransitiveThroughSameAssembly()
    {
        return VerifyAsync(
            """
            internal abstract class {|E128219:Shape|}
            {
            }

            internal abstract class {|E128219:Round|} : Shape
            {
            }

            internal sealed class Circle : Round
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnInterfaceImplementationCoexistsWithDerivation()
    {
        return VerifyAsync(
            """
            internal interface IRenderable
            {
            }

            internal abstract class {|E128219:Shape|}
            {
            }

            internal sealed class Circle : Shape, IRenderable
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBaseHasMembers()
    {
        return VerifyAsync(
            """
            internal abstract class Shape
            {
                internal abstract int Area();
            }

            internal sealed class Circle : Shape
            {
                internal override int Area() => 1;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBaseIsAlreadyClosed()
    {
        return VerifyAsync(
            """
            internal closed class Shape
            {
            }

            internal sealed class Circle : Shape
            {
            }
            """,
            null,
            LanguageVersion.Preview);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBaseIsPublic()
    {
        return VerifyAsync(
            """
            public abstract class Shape
            {
            }

            internal sealed class Circle : Shape
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNothingDerivesFromTheBase()
    {
        return VerifyAsync(
            """
            internal abstract class Shape
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAPositionalRecordSynthesizesMembers()
    {
        return VerifyAsync(
            """
            internal abstract record Shape(int Sides);

            internal sealed record Circle() : Shape(1);
            """);
    }
}
