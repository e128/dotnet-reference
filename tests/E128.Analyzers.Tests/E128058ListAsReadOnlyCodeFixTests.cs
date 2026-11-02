using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128058ListAsReadOnlyCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<ListAsReadOnlyAnalyzer, ListAsReadOnlyCodeFixProvider>(source, fixedCode);

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
