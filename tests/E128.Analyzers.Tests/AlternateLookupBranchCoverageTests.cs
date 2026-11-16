using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AlternateLookupBranchCoverageTests
{
    private static Task VerifyAsync(string members)
    {
        var code = "using System;\nusing System.Collections.Concurrent;\nusing System.Collections.Generic;\n\n"
                   + "class C\n{\n"
                   + members
                   + "}\n";
        return AnalyzerTestRunner.VerifyAsync<AlternateLookupAnalyzer>(code);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("")]
    [InlineData("StringComparer.Ordinal")]
    [InlineData("EqualityComparer<string>.Default")]
    public Task Reports_ForALocalWithADefaultOrOrdinalComparer(string comparer)
    {
        return VerifyAsync($$"""
                int Lookup(ReadOnlySpan<char> key)
                {
                    var cache = new Dictionary<string, int>({{comparer}});
                    return cache[{|E128133:new string(key)|}];
                }

            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("StringComparer.OrdinalIgnoreCase")]
    [InlineData("StringComparer.InvariantCulture")]
    [InlineData("comparer")]
    [InlineData("4, StringComparer.Ordinal")]
    public Task Silent_ForALocalWithAnotherComparer(string comparer)
    {
        return VerifyAsync($$"""
                int Lookup(ReadOnlySpan<char> key, IEqualityComparer<string> comparer)
                {
                    var cache = new Dictionary<string, int>({{comparer}});
                    return cache[new string(key)];
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAFieldHoldingACollectionExpression()
    {
        return VerifyAsync("""
                private readonly HashSet<string> _names = [];

                bool Has(ReadOnlySpan<char> key)
                {
                    return _names.Contains({|E128133:new string(key)|});
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForConcurrentDictionaryLookups()
    {
        return VerifyAsync("""
                private readonly ConcurrentDictionary<string, int> _cache = new();

                int Lookup(ReadOnlySpan<char> key)
                {
                    _ = _cache.TryGetValue({|E128133:new string(key)|}, out var value);
                    return _cache[{|E128133:new string(key)|}] + value;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAddAndRemoveOnAStringSet()
    {
        return VerifyAsync("""
                private readonly HashSet<string> _names = new();

                void Update(ReadOnlySpan<char> key)
                {
                    _ = _names.Add({|E128133:new string(key)|});
                    _ = _names.Remove({|E128133:new string(key)|});
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsAParameterOrHasNoInitializer()
    {
        return VerifyAsync("""
                int FromParameter(Dictionary<string, int> cache, ReadOnlySpan<char> key)
                {
                    return cache[new string(key)];
                }

                int NoInitializer(ReadOnlySpan<char> key)
                {
                    Dictionary<string, int> cache;
                    cache = new();
                    return cache[new string(key)];
                }

                int FromCall(ReadOnlySpan<char> key)
                {
                    var cache = Create();
                    return cache[new string(key)];
                }

                static Dictionary<string, int> Create() => new();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsNotAPlainName()
    {
        return VerifyAsync("""
                private readonly Dictionary<string, int> _cache = new();

                Dictionary<string, int> Get() => _cache;

                int Lookup(ReadOnlySpan<char> key)
                {
                    return this._cache[new string(key)] + Get()[new string(key)];
                }

                bool Has(ReadOnlySpan<char> key)
                {
                    return this._cache.ContainsKey(new string(key));
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCollectionIsNotStringKeyed()
    {
        return VerifyAsync("""
                private readonly Dictionary<int, int> _numbers = new();

                private readonly HashSet<int> _set = new();

                private readonly List<string> _list = new();

                bool Has(ReadOnlySpan<char> key)
                {
                    return _numbers.ContainsKey(new string(key).Length)
                        || _set.Contains(new string(key).Length)
                        || _list.Contains(new string(key));
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMemberTakesAValueOrNoArguments()
    {
        return VerifyAsync("""
                private readonly Dictionary<string, int> _cache = new();

                bool Has(ReadOnlySpan<char> key)
                {
                    _cache.Clear();
                    return _cache.ContainsValue(new string(key).Length);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyDoesNotCopyAWholeSpanOrArray()
    {
        return VerifyAsync("""
                private readonly Dictionary<string, int> _cache = new();

                int Lookup(Span<char> mutable, char letter, string text, ReadOnlySpan<char> span)
                {
                    return _cache[new string(mutable)]
                        + _cache[new string(letter, 3)]
                        + _cache[new string(span).Trim()]
                        + _cache[new(span)]
                        + _cache[text];
                }

            """);
    }
}
