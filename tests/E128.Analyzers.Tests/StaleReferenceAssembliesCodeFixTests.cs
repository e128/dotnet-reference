using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaleReferenceAssembliesCodeFixTests
{
    private static readonly ReferenceAssemblies Net100WithTesting = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.CodeAnalysis.CSharp.CodeFix.Testing", "1.1.3")]);

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<StaleReferenceAssembliesAnalyzer, StaleReferenceAssembliesCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = Net100WithTesting,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixNet80_TransformsToNet100()
    {
        return VerifyFixAsync(
            """
            using Microsoft.CodeAnalysis.Testing;
            class Tests
            {
                private static readonly ReferenceAssemblies Assemblies = {|E128062:ReferenceAssemblies.Net.Net80|};
            }
            """,
            """
            using Microsoft.CodeAnalysis.Testing;
            class Tests
            {
                private static readonly ReferenceAssemblies Assemblies = ReferenceAssemblies.Net.Net100;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixNet90_TransformsToNet100()
    {
        return VerifyFixAsync(
            """
            using Microsoft.CodeAnalysis.Testing;
            class Tests
            {
                private static readonly ReferenceAssemblies Assemblies = {|E128062:ReferenceAssemblies.Net.Net90|};
            }
            """,
            """
            using Microsoft.CodeAnalysis.Testing;
            class Tests
            {
                private static readonly ReferenceAssemblies Assemblies = ReferenceAssemblies.Net.Net100;
            }
            """);
    }
}
