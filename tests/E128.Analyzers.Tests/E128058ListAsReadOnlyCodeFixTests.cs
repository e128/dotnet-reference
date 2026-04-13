using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128058ListAsReadOnlyCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<ListAsReadOnlyAnalyzer, ListAsReadOnlyCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = 1,
        };
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListAsReadOnly_CodeFix_InsertsAsReadOnlyCall()
    {
        const string source = """
            using System.Collections.Generic;
            class Catalog
            {
                private readonly List<string> _items = new List<string>();
                public IReadOnlyList<string> Items => {|E128058:_items|};
            }
            """;

        const string fixedCode = """
            using System.Collections.Generic;
            class Catalog
            {
                private readonly List<string> _items = new List<string>();
                public IReadOnlyList<string> Items => _items.AsReadOnly();
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
