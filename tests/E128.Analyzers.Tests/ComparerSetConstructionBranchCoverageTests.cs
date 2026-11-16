using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ComparerSetConstructionBranchCoverageTests
{
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("while (index < items.Length)\n        {\n            set.Add(items[index++]);\n        }")]
    [InlineData("do\n        {\n            set.Add(items[index++]);\n        }\n        while (index < items.Length);")]
    public Task Reports_WhenAWhileOrDoLoopFillsTheSet(string loop)
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>($$"""
            using System;
            using System.Collections.Generic;

            class C
            {
                void M(string[] items)
                {
                    var index = 0;
                    var set = {|E128226:new HashSet<string>(StringComparer.Ordinal)|};
                    {{loop}}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSetIsOnlyReadAfterTheLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
            using System;
            using System.Collections.Generic;

            class C
            {
                bool M(string[] items, string key)
                {
                    var set = {|E128226:new HashSet<string>(StringComparer.Ordinal)|};
                    foreach (var item in items)
                    {
                        set.Add(item);
                    }

                    return set.Contains(key) && set.TryGetValue(key, out _) && set.Count > 0;
                }
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("_ = set.Remove(key);")]
    [InlineData("set.Clear();")]
    [InlineData("_ = set.Comparer;")]
    [InlineData("_ = set.Overlaps(items);")]
    public Task Silent_WhenTheSetIsUsedInAnyOtherWay(string use)
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>($$"""
            using System;
            using System.Collections.Generic;

            class C
            {
                void M(string[] items, string key)
                {
                    var set = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var item in items)
                    {
                        set.Add(item);
                    }

                    {{use}}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConstructorArgumentIsNotAComparer()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
            using System.Collections.Generic;

            class C
            {
                void M(string[] items, List<string> seed)
                {
                    var fromList = new HashSet<string>(seed);
                    var sized = new HashSet<string>(16);
                    foreach (var item in items)
                    {
                        fromList.Add(item);
                        sized.Add(item);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSetIsAssignedAfterItsDeclaration()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
            using System;
            using System.Collections.Generic;

            class C
            {
                void M(string[] items)
                {
                    HashSet<string> set;
                    set = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var item in items)
                    {
                        set.Add(item);
                    }
                }
            }
            """);
    }
}
