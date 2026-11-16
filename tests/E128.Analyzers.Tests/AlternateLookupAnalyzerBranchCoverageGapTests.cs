using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AlternateLookupAnalyzerBranchCoverageGapTests
{
    private static Task VerifyAsync(string members)
    {
        var code = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n"
                   + "class C\n{\n"
                   + members
                   + "}\n";
        return AnalyzerTestRunner.VerifyAsync<AlternateLookupAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsAnArray()
    {
        return VerifyAsync("""
                bool Has(string[] names, ReadOnlySpan<char> key)
                {
                    var local = names;
                    return local.Contains(new string(key));
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyIsAnotherTypeWithAStringConversion()
    {
        return VerifyAsync("""
                private readonly Dictionary<string, int> _cache = new();

                int Lookup(ReadOnlySpan<char> key)
                {
                    return _cache[new Wrapper(key)];
                }

                int LookupWithoutArguments()
                {
                    return _cache[new Wrapper { }];
                }

                sealed class Wrapper
                {
                    public Wrapper()
                    {
                    }

                    public Wrapper(ReadOnlySpan<char> text)
                    {
                    }

                    public static implicit operator string(Wrapper wrapper) => string.Empty;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsNotDeclaredByAVariableDeclarator()
    {
        return VerifyAsync("""
                int Lookup(List<Dictionary<string, int>> caches, ReadOnlySpan<char> key)
                {
                    var total = 0;
                    foreach (var cache in caches)
                    {
                        total += cache[new string(key)];
                    }

                    return total;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheInitializerHasNoArgumentList()
    {
        return VerifyAsync("""
                int Lookup(ReadOnlySpan<char> key)
                {
                    var cache = new Dictionary<string, int> { ["a"] = 1 };
                    return cache[{|E128133:new string(key)|}];
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheComparerIsAnotherPropertyOrType()
    {
        return VerifyAsync("""
                int WithObjectComparer(ReadOnlySpan<char> key)
                {
                    var cache = new Dictionary<string, int>(EqualityComparer<object>.Default);
                    return cache[new string(key)];
                }

                int WithInstanceProperty(ReadOnlySpan<char> key)
                {
                    var cache = new Dictionary<string, int>(Custom.Instance);
                    return cache[new string(key)];
                }

                int WithNonGenericDefault(ReadOnlySpan<char> key)
                {
                    var cache = new Dictionary<string, int>(Plain.Default);
                    return cache[new string(key)];
                }

                int WithGenericDefault(ReadOnlySpan<char> key)
                {
                    var cache = new Dictionary<string, int>(Generic<int>.Default);
                    return cache[new string(key)];
                }

                sealed class Custom : IEqualityComparer<string>
                {
                    public static Custom Instance { get; } = new();

                    public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);

                    public int GetHashCode(string obj) => obj.Length;
                }

                sealed class Plain : IEqualityComparer<string>
                {
                    public static Plain Default { get; } = new();

                    public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);

                    public int GetHashCode(string obj) => obj.Length;
                }

                sealed class Generic<T> : IEqualityComparer<string>
                {
                    public static Generic<T> Default { get; } = new();

                    public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);

                    public int GetHashCode(string obj) => obj.Length;
                }

            """);
    }
}
