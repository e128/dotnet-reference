using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MutableCollectionExposureE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<MutableCollectionExposureAnalyzer, MutableCollectionExposureCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListReturnToIReadOnlyList_Fixed()
    {
        const string source = """
                              using System.Collections.Generic;

                              public class Service
                              {
                                  public List<string> {|E128052:GetNames|}() => new List<string>();
                              }
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;

                                 public class Service
                                 {
                                     public IReadOnlyList<string> GetNames() => new List<string>();
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DictionaryReturnToIReadOnlyDictionary_Fixed()
    {
        const string source = """
                              using System.Collections.Generic;

                              public class Service
                              {
                                  public Dictionary<string, int> {|E128052:GetCounts|}() => new Dictionary<string, int>();
                              }
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;

                                 public class Service
                                 {
                                     public IReadOnlyDictionary<string, int> GetCounts() => new Dictionary<string, int>();
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyListToIReadOnlyList_Fixed()
    {
        const string source = """
                              using System.Collections.Generic;

                              public class Service
                              {
                                  public List<int> {|E128052:Items|} { get; init; }
                              }
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;

                                 public class Service
                                 {
                                     public IReadOnlyList<int> Items { get; init; }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
