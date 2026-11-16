using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MissingTraitCategoryAnalyzerBranchCoverageMoreTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("xunit.v3.core", "3.2.2")]);

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>(
            code,
            options: new AnalyzerTestOptions
            {
                References = Net100WithXunit,
                CompilerDiagnostics = CompilerDiagnostics.None
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTraitAttributeDoesNotBind()
    {
        return VerifyAsync("""
            public sealed class MyTests
            {
                [{|E128073:Xunit.Fact|}]
                [Trait("Category", "CI")]
                public void Works() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTraitNameIsANullConstant()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class MyTests
            {
                [{|E128073:Fact|}]
                [Trait(null, "CI")]
                public void Works() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTraitNameIsNotAConstant()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class MyTests
            {
                private static readonly string Name = "Category";

                [{|E128073:Fact|}]
                [Trait(Name, "CI")]
                public void Works() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFactAttributeIsNotOnAMethod()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class MyTests
            {
                [Fact]
                public int Value { get; }
            }
            """);
    }
}
