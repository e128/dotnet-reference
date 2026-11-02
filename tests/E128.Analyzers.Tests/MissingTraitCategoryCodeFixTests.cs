using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MissingTraitCategoryCodeFixTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("xunit.v3.core", "3.2.2")]);

    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<MissingTraitCategoryAnalyzer, MissingTraitCategoryCodeFixProvider>(source, fixedCode, references: Net100WithXunit);

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AddsTrait_ToFactMethod()
    {
        return VerifyFixAsync(
            """
            using Xunit;
            public sealed class MyTests
            {
                [{|E128073:Fact|}]
                public void Should_Work() { }
            }
            """,
            """
            using Xunit;
            public sealed class MyTests
            {
                [Fact]
                [Trait("Category", "CI")]
                public void Should_Work() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheDocCommentOnce()
    {
        // The doc comment is leading trivia of the first attribute list, so copying that
        // trivia onto the new list would duplicate it.
        return VerifyFixAsync(
            """
            using Xunit;
            public sealed class MyTests
            {
                /// <summary>Covers the happy path.</summary>
                [{|E128073:Fact|}]
                public void Should_Work() { }
            }
            """,
            """
            using Xunit;
            public sealed class MyTests
            {
                /// <summary>Covers the happy path.</summary>
                [Fact]
                [Trait("Category", "CI")]
                public void Should_Work() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AddsTrait_ToTheoryMethod()
    {
        return VerifyFixAsync(
            """
            using Xunit;
            public sealed class MyTests
            {
                [{|E128073:Theory|}]
                [InlineData(1)]
                public void Should_Work(int x) { _ = x; }
            }
            """,
            """
            using Xunit;
            public sealed class MyTests
            {
                [Theory]
                [InlineData(1)]
                [Trait("Category", "CI")]
                public void Should_Work(int x) { _ = x; }
            }
            """);
    }
}
