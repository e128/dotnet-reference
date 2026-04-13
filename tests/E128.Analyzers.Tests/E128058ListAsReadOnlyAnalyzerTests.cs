using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128058ListAsReadOnlyAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ListAsReadOnlyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListAsReadOnly_Reports_WhenListFieldReturnedAsIReadOnlyList()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
            using System.Collections.Generic;
            class Catalog
            {
                private readonly List<string> _items = new List<string>();
                public IReadOnlyList<string> GetItems() => _items.AsReadOnly();
            }
            """);
    }
}
