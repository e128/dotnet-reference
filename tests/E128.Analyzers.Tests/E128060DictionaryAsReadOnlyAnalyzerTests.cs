using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128060DictionaryAsReadOnlyAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<DictionaryAsReadOnlyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DictionaryAsReadOnly_Reports_WhenDictionaryFieldReturnedAsIReadOnlyDictionary()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class Cache
                           {
                               private readonly Dictionary<string, int> _dict = new Dictionary<string, int>();
                               public IReadOnlyDictionary<string, int> Items => {|E128060:_dict|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DictionaryAsReadOnly_Reports_WhenNewDictionaryReturnedFromMethod()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class Cache
                           {
                               public IReadOnlyDictionary<string, int> Build() => {|E128060:new Dictionary<string, int>()|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DictionaryAsReadOnly_Reports_WhenReturnedFromAsyncMethod()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class Cache
                           {
                               private readonly Dictionary<string, int> _dict = new Dictionary<string, int>();
                               public async Task<IReadOnlyDictionary<string, int>> GetAsync()
                               {
                                   await Task.Yield();
                                   return {|E128060:_dict|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DictionaryAsReadOnly_NoReport_WhenReturnTypeIsNotReadOnly()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class Cache
                           {
                               private readonly Dictionary<string, int> _dict = new Dictionary<string, int>();
                               public Dictionary<string, int> Items => _dict;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DictionaryAsReadOnly_NoReport_WhenReturnedAsIDictionary()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class Cache
                           {
                               private readonly Dictionary<string, int> _dict = new Dictionary<string, int>();
                               public IDictionary<string, int> Items => _dict;
                           }
                           """);
    }
}
