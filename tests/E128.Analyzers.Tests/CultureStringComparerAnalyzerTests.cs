using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CultureStringComparerAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenADictionaryUsesTheCurrentCulture()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
                           using System;
                           using System.Collections.Generic;
                           class C
                           {
                               object M()
                               {
                                   return new Dictionary<string, int>({|E128132:StringComparer.CurrentCulture|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenASetUsesTheInvariantCultureAndIgnoresCase()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
                           using System;
                           using System.Collections.Generic;
                           class C
                           {
                               object M()
                               {
                                   return new HashSet<string>({|E128132:StringComparer.InvariantCultureIgnoreCase|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCreationIsTargetTyped()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
                           using System;
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   Dictionary<string, int> map = new({|E128132:StringComparer.CurrentCultureIgnoreCase|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheComparerIsAlreadyOrdinal()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
                           using System;
                           using System.Collections.Generic;
                           class C
                           {
                               object M()
                               {
                                   return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSortedCollectionUsesACultureComparer()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
                           using System;
                           using System.Collections.Generic;
                           class C
                           {
                               object M()
                               {
                                   return new SortedDictionary<string, int>({|E128132:StringComparer.CurrentCulture|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSortedSetUsesACultureComparer()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
                           using System;
                           using System.Collections.Generic;
                           class C
                           {
                               object M()
                               {
                                   return new SortedSet<string>({|E128132:StringComparer.CurrentCulture|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheComparerIsNotAStringComparer()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
                           using System;
                           using System.Collections.Generic;
                           class C
                           {
                               object M(IEqualityComparer<string> comparer)
                               {
                                   return new HashSet<string>(comparer);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsNotACollection()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
                           using System;
                           class C
                           {
                               object M()
                               {
                                   return new Holder(StringComparer.CurrentCulture);
                               }
                           }
                           class Holder
                           {
                               public Holder(StringComparer comparer)
                               {
                               }
                           }
                           """);
    }
}
