using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OutOfRepoResolverAssertionAnalyzerBranchCoverageMoreTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private const string Resolver = """
                                    public static class CssPathResolver
                                    {
                                        public static string Resolve(string name) => name;
                                    }
                                    """;

    private static Task VerifyAsync(string code, bool lenient = false)
    {
        var test = new CSharpAnalyzerTest<OutOfRepoResolverAssertionAnalyzer, DefaultVerifier>
        {
            TestCode = code + "\n" + Resolver,
            ReferenceAssemblies = Net100WithXunit
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", "is_global = true\ne128_out_of_repo_resolvers = CssPathResolver.Resolve"));

        if (lenient)
        {
            test.CompilerDiagnostics = CompilerDiagnostics.None;
        }

        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTestMethodHasNoBody()
    {
        return VerifyAsync("""
            using Xunit;

            public abstract class Subject
            {
                [Fact]
                public abstract void ResolvePath_ReturnsThePath();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnUnresolvedCallPrecedesTheResolver()
    {
        return VerifyAsync(
            """
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public void {|E128105:ResolvePath_ReturnsThePath|}()
                {
                    Missing();
                    var path = CssPathResolver.Resolve("alpha");
                    Assert.NotNull(path);
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenSeveralSkipGuardsPrecedeTheResolver()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public void ResolvePath_ReturnsThePath()
                {
                    Assert.SkipWhen(false, "first guard");
                    Assert.SkipUnless(true, "second guard");
                    var path = CssPathResolver.Resolve("alpha");
                    Assert.NotNull(path);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTestCarriesAnotherAttributeAndANonSkipArgument()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class Subject
            {
                [Trait("Category", "CI")]
                [Fact(DisplayName = "resolves the path")]
                public void {|E128105:ResolvePath_ReturnsThePath|}()
                {
                    var path = CssPathResolver.Resolve("alpha");
                    Assert.NotNull(path);
                }
            }
            """);
    }
}
