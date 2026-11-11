using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128058ListAsReadOnlyAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ListAsReadOnly_Reports_WhenListFieldReturnedAsIReadOnlyList()
    {
        return AnalyzerTestRunner.VerifyAsync<ListAsReadOnlyAnalyzer>("""
                           using System.Collections.Generic;
                           class Catalog
                           {
                               private readonly List<string> _items = new List<string>();
                               public IReadOnlyList<string> Items => {|E128058:_items|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListAsReadOnly_NoReport_WhenAsReadOnlyIsUsed()
    {
        return AnalyzerTestRunner.VerifyAsync<ListAsReadOnlyAnalyzer>("""
                           using System.Collections.Generic;
                           class Catalog
                           {
                               private readonly List<string> _items = new List<string>();
                               public IReadOnlyList<string> Items => _items.AsReadOnly();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListAsReadOnly_NoReport_WhenReturnTypeIsNotReadOnly()
    {
        return AnalyzerTestRunner.VerifyAsync<ListAsReadOnlyAnalyzer>("""
                           using System.Collections.Generic;
                           class Catalog
                           {
                               private readonly List<string> _items = new List<string>();
                               public List<string> Items => _items;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListAsReadOnly_NoReport_WhenReturnedFromMethod()
    {
        return AnalyzerTestRunner.VerifyAsync<ListAsReadOnlyAnalyzer>("""
                           using System.Collections.Generic;
                           class Catalog
                           {
                               private readonly List<string> _items = new List<string>();
                               public IReadOnlyList<string> GetItems() => _items.AsReadOnly();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListAsReadOnly_NoReport_WhenNewListReturnedFromMethod()
    {
        return AnalyzerTestRunner.VerifyAsync<ListAsReadOnlyAnalyzer>("""
                           using System.Collections.Generic;
                           class Catalog
                           {
                               public IReadOnlyList<string> Build() => new List<string>();
                           }
                           """);
    }
}
