using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AlternateLookupCodeFixProviderBranchCoverageGapTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheRemainingArgumentsOfAnInvocation()
    {
        return CodeFixTestRunner.RunAsync<AlternateLookupAnalyzer, AlternateLookupCodeFixProvider>(
            """
            using System;
            using System.Collections.Generic;

            class C
            {
                private readonly Dictionary<string, int> _cache = new();

                bool Add(ReadOnlySpan<char> key)
                {
                    return _cache.TryAdd({|E128133:new string(key)|}, 1);
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;

            class C
            {
                private readonly Dictionary<string, int> _cache = new();

                bool Add(ReadOnlySpan<char> key)
                {
                    return _cache.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(key, 1);
                }
            }
            """);
    }
}
