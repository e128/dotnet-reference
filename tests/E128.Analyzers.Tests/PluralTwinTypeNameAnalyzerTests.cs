using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PluralTwinTypeNameAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheNameIsThePluralOfASibling()
    {
        return AnalyzerTestRunner.VerifyAsync<PluralTwinTypeNameAnalyzer>("""
                           namespace N
                           {
                               public sealed class FetchCommand { }
                               public sealed class [|FetchCommands|] { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePluralTypeIsACollection()
    {
        return AnalyzerTestRunner.VerifyAsync<PluralTwinTypeNameAnalyzer>("""
                           namespace N
                           {
                               public sealed class Color { }

                               public sealed class Colors : System.Collections.Generic.List<Color> { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNoSingularSiblingExists()
    {
        return AnalyzerTestRunner.VerifyAsync<PluralTwinTypeNameAnalyzer>("""
                           namespace N
                           {
                               public sealed class FetchCommands { }
                           }
                           """);
    }
}
