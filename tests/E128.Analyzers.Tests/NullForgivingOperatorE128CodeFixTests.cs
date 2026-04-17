using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NullForgivingOperatorE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<NullForgivingOperatorAnalyzer, NullForgivingOperatorCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullForgiving_RemovesBangOperator()
    {
        const string source = """
                              #nullable enable
                              class C
                              {
                                  void M(string? s)
                                  {
                                      string x = {|E128043:s!|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 #nullable enable
                                 class C
                                 {
                                     void M(string? s)
                                     {
                                         string x = s;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullForgiving_OnReturn_RemovesBangOperator()
    {
        const string source = """
                              #nullable enable
                              class C
                              {
                                  string M(string? s) => {|E128043:s!|};
                              }
                              """;

        const string fixedCode = """
                                 #nullable enable
                                 class C
                                 {
                                     string M(string? s) => s;
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
