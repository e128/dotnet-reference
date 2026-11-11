using System.Threading.Tasks;
using E128.Analyzers.Style;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UntrackedTodoAnalyzerTests
{
    private static Task VerifyAsync(string code, string? pattern = null)
    {
        return AnalyzerTestRunner.VerifyAsync<UntrackedTodoAnalyzer>(
            code,
            options: new AnalyzerTestOptions
            {
                AnalyzerConfig = pattern is null
                    ? null
                    : ("/.editorconfig", "is_global = true\ne128_tracked_ref_pattern = " + pattern)
            });
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
    public Task MarkerInsideProse_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               // This avoids a hack in the parser, see the design note
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
