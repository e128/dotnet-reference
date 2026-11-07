using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImmutableArrayDefaultCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<ImmutableArrayDefaultAnalyzer, ImmutableArrayDefaultCodeFixProvider>(
            source,
            fixedCode,
            fixAllIterations: 1,
            behaviors: CodeFixTestBehaviors.SkipLocalDiagnosticCheck);

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

    [Fact]
    [Trait("Category", "CI")]
    public Task StructInstanceField_NoFix()
    {
        const string source = """
                              using System.Collections.Immutable;

                              struct S
                              {
                                  private ImmutableArray<int> {|E128163:values|};

                                  internal int M() => values.Length;
                              }
                              """;

        return CodeFixTestRunner.NoFixAsync<ImmutableArrayDefaultAnalyzer, ImmutableArrayDefaultCodeFixProvider>(
            source,
            behaviors: CodeFixTestBehaviors.SkipLocalDiagnosticCheck);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StructStaticField_FixInitializesToEmpty()
    {
        const string source = """
                              using System.Collections.Immutable;

                              struct S
                              {
                                  private static ImmutableArray<int> {|E128163:values|};

                                  internal int M() => values.Length;
                              }
                              """;

        const string fixedCode = """
                                 using System.Collections.Immutable;

                                 struct S
                                 {
                                     private static ImmutableArray<int> values = ImmutableArray<int>.Empty;

                                     internal int M() => values.Length;
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
