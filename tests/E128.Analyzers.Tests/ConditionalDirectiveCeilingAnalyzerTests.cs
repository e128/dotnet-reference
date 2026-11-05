using System.Threading.Tasks;
using E128.Analyzers.Maintainability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConditionalDirectiveCeilingAnalyzerTests
{
    private static Task VerifyAsync(string code, string? config = null)
    {
        var test = new CSharpAnalyzerTest<ConditionalDirectiveCeilingAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        if (config is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", "is_global = true\n" + config));
        }

        return test.RunAsync();
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
