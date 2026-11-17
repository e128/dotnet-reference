using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MultiStringEqualsOrChainComparerSpellingTests
{
    private static Task VerifyFixAsync(string source, string fixedCode, int fixAllIterations = 1)
    {
        return new CSharpCodeFixTest<MultiStringEqualsOrChainAnalyzer, MultiStringEqualsOrChainCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = fixAllIterations
        }.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     A fully qualified comparison keeps its case-insensitive meaning. Mapping it to the ordinal
    ///     comparer would turn case-insensitive matching into case-sensitive matching.
    /// </summary>
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("System.StringComparison.OrdinalIgnoreCase", "OrdinalIgnoreCase")]
    [InlineData("global::System.StringComparison.CurrentCultureIgnoreCase", "CurrentCultureIgnoreCase")]
    public Task QualifiedComparison_KeepsTheMatchingComparer(string comparison, string comparer)
    {
        var source = $$"""
                       class C
                       {
                           bool M(string text) =>
                               {|E128029:string.Equals(text, "a", {{comparison}}) || string.Equals(text, "b", {{comparison}}) || string.Equals(text, "c", {{comparison}})|};
                       }
                       """;

        var fixedCode = $$"""
                          using System;
                          using System.Collections.Generic;

                          class C
                          {
                              private static readonly HashSet<string> _textValues = new(StringComparer.{{comparer}}) { "a", "b", "c" };

                              bool M(string text) =>
                                  _textValues.Contains(text);
                          }
                          """;

        return VerifyFixAsync(source, fixedCode);
    }

    /// <summary>
    ///     A comparison held in a variable cannot move into a static field, so no fix is offered.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task VariableComparison_NoFixApplied()
    {
        const string source = """
                              using System;
                              class C
                              {
                                  bool M(string text, StringComparison comparison) =>
                                      {|E128029:string.Equals(text, "a", comparison) || string.Equals(text, "b", comparison) || string.Equals(text, "c", comparison)|};
                              }
                              """;

        return VerifyFixAsync(source, source, 0);
    }
}
