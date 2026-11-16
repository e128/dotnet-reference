using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the array origins and length guards that the split index rule reads.
/// </summary>
public sealed class UnguardedSplitIndexBranchCoverageTests
{
    private const string Header = """
                                  class Splitter
                                  {
                                      public string[] Split(char separator) => new[] { "a" };
                                  }

                                  """;

    private static Task VerifyAsync(string members, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>(
            Header + "class C\n{\n" + members + "\n}\n",
            expected: null,
            options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSplitBelongsToAnotherType()
    {
        return VerifyAsync("""
                               string M(Splitter splitter)
                               {
                                   return splitter.Split(',')[1];
                               }

                               string N(Splitter splitter)
                               {
                                   var parts = splitter.Split(',');
                                   return parts[1];
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalComesFromAnotherInvocation()
    {
        return VerifyAsync("""
                               string[] Make() => new[] { "a" };

                               string M()
                               {
                                   var parts = Make();
                                   return parts[1];
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalComesFromANonInvocation()
    {
        return VerifyAsync("""
                               string M()
                               {
                                   string[] parts = new[] { "a" };
                                   return parts[1];
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalHasNoInitializer()
    {
        return VerifyAsync("""
                               string M(string line)
                               {
                                   string[] parts;
                                   parts = line.Split(',');
                                   return parts[1];
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsALoopVariable()
    {
        return VerifyAsync("""
                               string M(string[][] rows)
                               {
                                   foreach (var parts in rows)
                                   {
                                       return parts[1];
                                   }

                                   return string.Empty;
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArrayIsAParameterOrAField()
    {
        return VerifyAsync("""
                               private readonly string[] _parts = new[] { "a" };

                               string M(string[] parts) => parts[1];

                               string N() => _parts[1];
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_TheLocalThatHoldsTheSplitInAMultiDeclarator()
    {
        return VerifyAsync("""
                               string M(string line)
                               {
                                   string[] first = line.Split(','), second = line.Split(';');
                                   return {|E128170:second[1]|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyAnotherArrayHasItsLengthRead()
    {
        return VerifyAsync("""
                               string M(string line, string[] other)
                               {
                                   var parts = line.Split(',');
                                   if (other.Length > 2)
                                   {
                                       return {|E128170:parts[1]|};
                                   }

                                   return string.Empty;
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheMemberAccessBeforeTheIndexIsNotLength()
    {
        return VerifyAsync("""
                               string M(string line)
                               {
                                   var parts = line.Split(',');
                                   var count = parts.Rank;
                                   return {|E128170:parts[1]|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheNullConditionalReadIsNotLength()
    {
        return VerifyAsync("""
                               string M(string line)
                               {
                                   var parts = line.Split(',');
                                   var rank = parts?.Rank;
                                   var first = parts?[0];
                                   return {|E128170:parts[1]|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePatternBeforeTheIndexChecksNoLength()
    {
        return VerifyAsync("""
                               string M(string line)
                               {
                                   var parts = line.Split(',');
                                   var notNull = parts is not null;
                                   var typed = parts is string[] copy;
                                   var ranked = parts is { Rank: 1 };
                                   return {|E128170:parts[1]|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALengthPropertyPatternPrecedesTheIndex()
    {
        return VerifyAsync("""
                               string M(string line)
                               {
                                   var parts = line.Split(',');
                                   if (parts is { Length: > 1 })
                                   {
                                       return parts[1];
                                   }

                                   return string.Empty;
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAPositionalPatternHasNoPropertyClause()
    {
        return VerifyAsync(
            """
                string M(string line)
                {
                    var parts = line.Split(',');
                    var matched = parts is string[] ();
                    return {|E128170:parts[1]|};
                }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
