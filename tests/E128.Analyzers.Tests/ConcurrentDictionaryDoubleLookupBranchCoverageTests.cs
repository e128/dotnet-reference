using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrentDictionaryDoubleLookupBranchCoverageTests
{
    private static Task VerifyAsync(string body)
    {
        var code = "using System.Collections.Concurrent;\n\n"
                   + "class Own : ConcurrentDictionary<string, int>\n{\n"
                   + "    public bool Has(string key)\n    {\n        return ContainsKey(key);\n    }\n}\n\n"
                   + "class C\n{\n"
                   + "    static string GetKey() => \"k\";\n\n"
                   + "    int M(ConcurrentDictionary<string, int> map, ConcurrentDictionary<string, int> other, string key, int index)\n    {\n"
                   + body
                   + "        return 0;\n    }\n}\n";
        return AnalyzerTestRunner.VerifyAsync<ConcurrentDictionaryDoubleLookupAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheIndexerBelongsToAnotherDictionary()
    {
        return VerifyAsync("""
                if (map.ContainsKey(key))
                {
                    return other[key];
                }

        """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("map[key]++;")]
    [InlineData("++map[key];")]
    [InlineData("map[key]--;")]
    [InlineData("--map[key];")]
    [InlineData("map[key] += 1;")]
    public Task Silent_WhenTheIndexerIsWritten(string write)
    {
        return VerifyAsync($$"""
                if (map.ContainsKey(key))
                {
                    {{write}}
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheIndexerValueIsReadIntoAnAssignment()
    {
        return VerifyAsync("""
                var value = 0;
                if ({|E128208:map.ContainsKey(key)|})
                {
                    value = map[key];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheKeyIsTheSameCall()
    {
        return VerifyAsync("""
                if ({|E128208:map.ContainsKey(GetKey())|})
                {
                    return map[GetKey()];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeysAreDifferentConstants()
    {
        return VerifyAsync("""
                if (map.ContainsKey("a"))
                {
                    return map["b"];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCheckIsNegatedOrCombined()
    {
        return VerifyAsync("""
                if (!map.ContainsKey(key))
                {
                    return map[key];
                }

                if (map.ContainsKey(key) && index > 0)
                {
                    return map[key];
                }

        """);
    }
}
