using System.Threading.Tasks;
using E128.Analyzers.Maintainability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConditionalDirectiveCeilingAnalyzerTests
{
    private static Task VerifyAsync(string code, string? config = null)
    {
        return AnalyzerTestRunner.VerifyAsync<ConditionalDirectiveCeilingAnalyzer>(code, options: new AnalyzerTestOptions { AnalyzerConfig = ("/.globalconfig", "is_global = true\n" + config) });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFilePassesTheDefaultCeiling()
    {
        return VerifyAsync("""
                           class C
                           {
                           #if FOO
                               int A() => 1;
                           #endif
                           #if FOO
                               int B() => 1;
                           #endif
                           #if FOO
                               int D() => 1;
                           #endif
                           #if FOO
                               int E() => 1;
                           #endif
                           {|E128201:#if FOO|}
                               int F() => 1;
                           #endif
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFileMatchesTheDefaultCeiling()
    {
        return VerifyAsync("""
                           class C
                           {
                           #if FOO
                               int A() => 1;
                           #endif
                           #if FOO
                               int B() => 1;
                           #endif
                           #if FOO
                               int D() => 1;
                           #endif
                           #if FOO
                               int E() => 1;
                           #endif
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheConfiguredCeilingIsLower()
    {
        return VerifyAsync(
            """
            class C
            {
            #if FOO
                int A() => 1;
            #endif
            {|E128201:#if FOO|}
                int B() => 1;
            #endif
            }
            """,
            "e128_max_conditional_directives = 1");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNoDirectiveIsPresent()
    {
        return VerifyAsync("""
                           class C
                           {
                               int A() => 1;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FallsBackToTheDefault_WhenTheOptionIsNotANumber()
    {
        return VerifyAsync(
            """
            class C
            {
            #if FOO
                int A() => 1;
            #endif
            #if FOO
                int B() => 1;
            #endif
            #if FOO
                int D() => 1;
            #endif
            #if FOO
                int E() => 1;
            #endif
            {|E128201:#if FOO|}
                int F() => 1;
            #endif
            }
            """,
            "e128_max_conditional_directives = many");
    }
}
