using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128060DictionaryAsReadOnlyCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<DictionaryAsReadOnlyAnalyzer, DictionaryAsReadOnlyCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions { FixAllIterations = 1 });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DictionaryAsReadOnly_CodeFix_InsertsAsReadOnlyCall()
    {
        const string source = """
                              using System.Collections.Generic;
                              class Cache
                              {
                                  private readonly Dictionary<string, int> _dict = new Dictionary<string, int>();
                                  public IReadOnlyDictionary<string, int> Items => {|E128060:_dict|};
                              }
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;
                                 class Cache
                                 {
                                     private readonly Dictionary<string, int> _dict = new Dictionary<string, int>();
                                     public IReadOnlyDictionary<string, int> Items => _dict.AsReadOnly();
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
