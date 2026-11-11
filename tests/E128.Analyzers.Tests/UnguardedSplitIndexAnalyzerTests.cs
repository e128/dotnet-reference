using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnguardedSplitIndexAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task InlineSplitIndexOne_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               string M(string line) => {|E128170:line.Split(',')[1]|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalSplitIndexTwoWithoutGuard_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               string M(string line)
                               {
                                   var parts = line.Split(':');
                                   return {|E128170:parts[2]|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LengthReadAfterIndex_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               int M(string line)
                               {
                                   var parts = line.Split(':');
                                   var value = {|E128170:parts[1]|};
                                   return parts.Length + value.Length;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexZero_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               string M(string line) => line.Split(',')[0];
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FromEndIndex_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               string M(string line) => line.Split(',')[^1];
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LengthChecked_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               string? M(string line)
                               {
                                   var parts = line.Split(':');

                                   if (parts.Length < 2)
                                   {
                                       return null;
                                   }

                                   return parts[1];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonConstantIndexAndOtherArray_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               string M(string line, int i, string[] other) => line.Split(',')[i] + other[1];
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonStringSplit_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class Splitter
                           {
                               public string[] Split(char c) => new string[3];
                           }

                           class C
                           {
                               string M(Splitter s) => s.Split(',')[1];
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyPatternOnLength_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               string M(string line)
                               {
                                   var parts = line.Split(':');
                                   return parts is { Length: >= 2 } ? parts[1] : "";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListPattern_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               string M(string line)
                               {
                                   var parts = line.Split(':');
                                   return parts is [_, _, ..] ? parts[1] : "";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullConditionalLengthRead_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               string M(string line)
                               {
                                   var parts = line.Split(':');
                                   if (parts?.Length < 2)
                                   {
                                       return "";
                                   }

                                   return parts[1];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PatternGuardAfterIndex_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UnguardedSplitIndexAnalyzer>("""
                           class C
                           {
                               bool M(string line)
                               {
                                   var parts = line.Split(':');
                                   var value = {|E128170:parts[1]|};
                                   return parts is { Length: >= 2 } && value.Length > 0;
                               }
                           }
                           """);
    }
}
