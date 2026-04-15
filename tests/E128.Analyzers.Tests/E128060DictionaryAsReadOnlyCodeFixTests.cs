using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128060DictionaryAsReadOnlyCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<DictionaryAsReadOnlyAnalyzer, DictionaryAsReadOnlyCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            // Dictionary<K,V>.AsReadOnly() requires .NET 9+
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1,
        };
        return test.RunAsync();
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
