using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ComparerSetConstructionAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAForeachLoopFillsTheSet()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               void M(string[] items)
                               {
                                   var set = {|E128226:new HashSet<string>(StringComparer.OrdinalIgnoreCase)|};
                                   foreach (var item in items)
                                   {
                                       set.Add(item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAForLoopFillsTheSet()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               void M(string[] items)
                               {
                                   var set = {|E128226:new HashSet<string>(StringComparer.Ordinal)|};
                                   for (var i = 0; i < items.Length; i++)
                                   {
                                       set.Add(items[i]);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenACollectionInitializerIsPresent()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               void M(string[] items)
                               {
                                   var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "seed" };
                                   foreach (var item in items)
                                   {
                                       set.Add(item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnAddSitsOutsideTheLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               void M(string[] items)
                               {
                                   var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                   set.Add("seed");
                                   foreach (var item in items)
                                   {
                                       set.Add(item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSetEscapesToAnotherMethod()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               void M(string[] items)
                               {
                                   var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                   foreach (var item in items)
                                   {
                                       set.Add(item);
                                   }

                                   Consume(set);
                               }

                               static void Consume(HashSet<string> set)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSetIsAFieldInitializer()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               private readonly HashSet<string> _set = new(StringComparer.OrdinalIgnoreCase);

                               void M(string[] items)
                               {
                                   foreach (var item in items)
                                   {
                                       _set.Add(item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreationIsADictionary()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               void M(string[] items)
                               {
                                   var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                                   foreach (var item in items)
                                   {
                                       map.Add(item, item.Length);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreationHasNoComparer()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
                           using System.Collections.Generic;

                           class C
                           {
                               void M(string[] items)
                               {
                                   var set = new HashSet<string>();
                                   foreach (var item in items)
                                   {
                                       set.Add(item);
                                   }
                               }
                           }
                           """);
    }
}
