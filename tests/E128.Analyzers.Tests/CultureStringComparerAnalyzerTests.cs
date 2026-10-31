using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CultureStringComparerAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<CultureStringComparerAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenADictionaryUsesTheCurrentCulture()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
    public Task Silent_WhenTheCollectionSortsByCulture()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           class C
                           {
                               object M()
                               {
                                   return new SortedDictionary<string, int>(StringComparer.CurrentCulture);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheComparerIsNotAStringComparer()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
