using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AbstractBaseWithMembersAnalyzerTests
{
    private static Task VerifyAsync(
        string code,
        DiagnosticResult[]? expected = null,
        LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        return AnalyzerTestRunner.VerifyAsync<AbstractBaseWithMembersAnalyzer>(
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
    public Task Reports_WhenTheBaseDeclaresAConcreteMember()
    {
        return VerifyAsync(
            """
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
    public Task Reports_WhenTheBaseDeclaresAnAbstractMember()
    {
        return VerifyAsync(
            """
            internal abstract class {|E128225:Shape|}
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
    public Task Reports_WhenTheBaseDeclaresAConstructor()
    {
        return VerifyAsync(
            """
            internal abstract class {|E128225:Shape|}
            {
                private protected Shape()
                {
                }
            }

            internal sealed class Circle : Shape
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBaseIsMemberless()
    {
        return VerifyAsync(
            """
            internal abstract class Shape
            {
            }

            internal sealed class Circle : Shape
            {
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
                internal string Label() => "shape";
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
    public Task Silent_WhenNothingDerivesFromTheBase()
    {
        return VerifyAsync(
            """
            internal abstract class Shape
            {
                internal string Label() => "shape";
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBaseIsARecord()
    {
        return VerifyAsync(
            """
            internal abstract record Shape
            {
                internal string Label() => "shape";
            }

            internal sealed record Circle : Shape;
            """);
    }
}
