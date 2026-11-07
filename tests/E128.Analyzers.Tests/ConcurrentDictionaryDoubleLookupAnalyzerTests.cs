using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrentDictionaryDoubleLookupAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ContainsKey_ThenIndexer_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrentDictionaryDoubleLookupAnalyzer>("""
            using System.Collections.Concurrent;
            class C
            {
                int M(ConcurrentDictionary<string, int> map, string key)
                {
                    if ({|E128208:map.ContainsKey(key)|})
                    {
                        return map[key];
                    }

                    return 0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ContainsKey_OutsideIf_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrentDictionaryDoubleLookupAnalyzer>("""
            using System.Collections.Concurrent;
            class C
            {
                bool M(ConcurrentDictionary<string, int> map, string key)
                {
                    return map.ContainsKey(key);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Dictionary_NotConcurrent_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrentDictionaryDoubleLookupAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                int M(Dictionary<string, int> map, string key)
                {
                    if (map.ContainsKey(key))
                    {
                        return map[key];
                    }

                    return 0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DifferentKey_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrentDictionaryDoubleLookupAnalyzer>("""
            using System.Collections.Concurrent;
            class C
            {
                int M(ConcurrentDictionary<string, int> map, string key, string other)
                {
                    if (map.ContainsKey(key))
                    {
                        return map[other];
                    }

                    return 0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexerWrite_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrentDictionaryDoubleLookupAnalyzer>("""
            using System.Collections.Concurrent;
            class C
            {
                void M(ConcurrentDictionary<string, int> map, string key)
                {
                    if (map.ContainsKey(key))
                    {
                        map[key] = 1;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralKey_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrentDictionaryDoubleLookupAnalyzer>("""
            using System.Collections.Concurrent;
            class C
            {
                int M(ConcurrentDictionary<string, int> map)
                {
                    if ({|E128208:map.ContainsKey("key")|})
                    {
                        return map["key"];
                    }

                    return 0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParenthesizedCondition_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrentDictionaryDoubleLookupAnalyzer>("""
            using System.Collections.Concurrent;
            class C
            {
                int M(ConcurrentDictionary<string, int> map, string key)
                {
                    if (({|E128208:map.ContainsKey(key)|}))
                    {
                        return map[key];
                    }

                    return 0;
                }
            }
            """);
    }
}
