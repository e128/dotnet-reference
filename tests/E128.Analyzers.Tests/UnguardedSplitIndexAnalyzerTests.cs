using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnguardedSplitIndexAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<UnguardedSplitIndexAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InlineSplitIndexOne_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
    public Task IndexZero_Silent()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
}
