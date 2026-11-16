using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MissingTraitCategoryAnalyzerBranchCoverageTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("xunit.v3.core", "3.2.2")]);

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<MissingTraitCategoryAnalyzer>(
            code,
            options: new AnalyzerTestOptions { References = Net100WithXunit });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTraitNameIsNotCategory()
    {
        return VerifyAsync("""
            using Xunit;
            public sealed class MyTests
            {
                [{|E128073:Fact|}]
                [Trait("Priority", "High")]
                public void Works() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTraitNameHasDifferentCase()
    {
        return VerifyAsync("""
            using Xunit;
            public sealed class MyTests
            {
                [{|E128073:Fact|}]
                [Trait("category", "CI")]
                public void Works() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnAttributeNamedTraitIsNotTheXunitTrait()
    {
        return VerifyAsync("""
            using System;
            using Xunit;

            namespace Other
            {
                sealed class TraitAttribute : Attribute
                {
                    public TraitAttribute(string name, string value) { }
                }
            }

            public sealed class MyTests
            {
                [{|E128073:Fact|}]
                [Other.Trait("Category", "CI")]
                public void Works() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAConstantNamesTheCategory()
    {
        return VerifyAsync("""
            using Xunit;
            public sealed class MyTests
            {
                private const string Key = "Category";

                [Fact]
                [Trait(Key, "CI")]
                public void Works() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheClassHasSeveralAttributesIncludingTheCategory()
    {
        return VerifyAsync("""
            using System;
            using Xunit;

            [Obsolete]
            [Trait("Priority", "High")]
            [TraitAttribute("Category", "CI")]
            public sealed class MyTests
            {
                [Fact]
                public void Works() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserDefinedFactAttribute()
    {
        return VerifyAsync("""
            using System;
            namespace Other
            {
                sealed class FactAttribute : Attribute { }
            }

            public sealed class MyTests
            {
                [Other.Fact]
                public void Works() { }
            }
            """);
    }
}
