using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AlternateLookupCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<AlternateLookupAnalyzer, AlternateLookupCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheIndexerKeyWithTheSpan()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Collections.Generic;

            class C
            {
                private readonly Dictionary<string, int> _cache = new();

                int Lookup(ReadOnlySpan<char> key)
                {
                    return _cache[{|E128133:new string(key)|}];
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;

            class C
            {
                private readonly Dictionary<string, int> _cache = new();

                int Lookup(ReadOnlySpan<char> key)
                {
                    return _cache.GetAlternateLookup<ReadOnlySpan<char>>()[key];
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheContainsKeyArgument()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Collections.Generic;

            class C
            {
                private readonly HashSet<string> _names = new();

                bool Has(string text)
                {
                    return _names.Contains({|E128133:new string(text.AsSpan())|});
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;

            class C
            {
                private readonly HashSet<string> _names = new();

                bool Has(string text)
                {
                    return _names.GetAlternateLookup<ReadOnlySpan<char>>().Contains(text.AsSpan());
                }
            }
            """);
    }
}
