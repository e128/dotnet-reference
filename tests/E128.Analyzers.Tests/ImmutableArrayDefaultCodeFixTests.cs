using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImmutableArrayDefaultCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<ImmutableArrayDefaultAnalyzer, ImmutableArrayDefaultCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1,
            // CompilationEnd diagnostics are non-local by design.
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnassignedField_FixInitializesToEmpty()
    {
        const string source = """
                              using System.Collections.Immutable;

                              class C
                              {
                                  private ImmutableArray<int> {|E128163:values|};

                                  internal int M() => values.Length;
                              }
                              """;

        const string fixedCode = """
                                 using System.Collections.Immutable;

                                 class C
                                 {
                                     private ImmutableArray<int> values = ImmutableArray<int>.Empty;

                                     internal int M() => values.Length;
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
