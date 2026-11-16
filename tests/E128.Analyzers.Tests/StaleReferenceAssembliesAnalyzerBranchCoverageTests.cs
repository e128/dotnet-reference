using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaleReferenceAssembliesAnalyzerBranchCoverageTests
{
    private const string Stubs = """
        class ReferenceAssemblies
        {
            public static class Net
            {
                public static int Net80 => 80;
                public static int Net100 => 100;
                public static int Unknown => 0;
                public static int Net90Field;
                public static int Net90() => 90;
            }

            public static class Other
            {
                public static int Net80 => 80;
            }
        }

        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<StaleReferenceAssembliesAnalyzer>(Stubs + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnUnknownVersionAndAnUpToDateVersion()
    {
        return VerifyAsync("""
            class C
            {
                int M() => ReferenceAssemblies.Net.Unknown + ReferenceAssemblies.Net.Net100;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnOuterNameOrMiddleNameThatDoesNotMatch()
    {
        return VerifyAsync("""
            class C
            {
                int M() => ReferenceAssemblies.Other.Net80;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAFieldOrMethodNamedLikeAVersion()
    {
        return VerifyAsync("""
            class C
            {
                int M() => ReferenceAssemblies.Net.Net90() + ReferenceAssemblies.Net.Net90Field;
            }
            """);
    }

}
