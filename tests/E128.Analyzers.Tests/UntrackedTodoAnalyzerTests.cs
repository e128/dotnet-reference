using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UntrackedTodoAnalyzerTests
{
    private static Task VerifyAsync(string code, string? pattern = null)
    {
        var test = new CSharpAnalyzerTest<UntrackedTodoAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };

        if (pattern is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(
                ("/.editorconfig", "is_global = true\ne128_tracked_ref_pattern = " + pattern));
        }

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TodoWithoutReference_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               {|E128167:// TODO: handle the empty case|}
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixmeAndHack_Fire()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   {|E128167:// FIXME this leaks|}
                                   {|E128167:/* hack: retry twice */|}
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TodoWithAzureDevOpsReference_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               // TODO(AB#1234): handle the empty case
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TodoWithIssueNumber_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               // FIXME #88 leaks a handle
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WordInsideAnotherWord_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               // Mastodon and hackathon notes
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CustomPattern_AcceptsMatchingReference()
    {
        return VerifyAsync("""
                           class C
                           {
                               // TODO PROJ-42 tidy up
                               {|E128167:// TODO AB#42 tidy up|}
                               void M() { }
                           }
                           """, @"PROJ-\d+");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InvalidPattern_FallsBackToDefault()
    {
        return VerifyAsync("""
                           class C
                           {
                               // TODO #7 done
                               {|E128167:// TODO none|}
                               void M() { }
                           }
                           """, "([unclosed");
    }
}
