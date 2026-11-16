using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrentDictionaryDoubleLookupAnalyzerBranchCoverageMoreTests
{
    private static string Wrap(string body)
    {
        return "using System.Collections.Concurrent;\n\n"
               + "class C\n{\n"
               + "    int M(ConcurrentDictionary<string, int> map, ConcurrentDictionary<string, int> other, string key, int index)\n    {\n"
               + body
               + "        return 0;\n    }\n}\n";
    }

    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrentDictionaryDoubleLookupAnalyzer>(Wrap(body));
    }

    private static Task VerifyLenientAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrentDictionaryDoubleLookupAnalyzer>(
            Wrap(body),
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsANamespace()
    {
        return VerifyLenientAsync("""
                if (System.ContainsKey(key))
                {
                    return map[key];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenContainsKeyTakesNoArgument()
    {
        return VerifyLenientAsync("""
                if (map.ContainsKey())
                {
                    return map[key];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsAConditionalExpression()
    {
        return VerifyAsync("""
                if ((index > 0 ? map : other).ContainsKey(key))
                {
                    return map[key];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheIndexerCallHasTwoArguments()
    {
        return VerifyLenientAsync("""
                if (map.ContainsKey(key))
                {
                    return map[key, index];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheIndexerValueIsNegated()
    {
        return VerifyAsync("""
                if ({|E128208:map.ContainsKey(key)|})
                {
                    return -map[key];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheIndexerValueIsReadThroughAParenthesizedExpression()
    {
        return VerifyAsync("""
                if ({|E128208:map.ContainsKey(key)|})
                {
                    return (map[key]) + 1;
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheKeysAreTheSameConstant()
    {
        return VerifyAsync("""
                if ({|E128208:map.ContainsKey("a")|})
                {
                    return map["a"];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeysAreNotConstantExpressions()
    {
        return VerifyAsync("""
                if (map.ContainsKey(index > 0 ? "a" : "b"))
                {
                    return map[index > 0 ? "a" : "b"];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyTheIndexerKeyIsAConstant()
    {
        return VerifyAsync("""
                if (map.ContainsKey(index > 0 ? "a" : "b"))
                {
                    return map["a"];
                }

        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyTheContainsKeyArgumentIsAConstant()
    {
        return VerifyAsync("""
                if (map.ContainsKey("a"))
                {
                    return map[index > 0 ? "a" : "b"];
                }

        """);
    }
}
