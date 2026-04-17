using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexNestedE128CodeFixTests
{
    private const string PartialImpl = "    private static partial System.Text.RegularExpressions.Regex DigitsOnly() => null!;";

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<GeneratedRegexAnalyzer, GeneratedRegexNestedCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedQuantifier_DotPlusPlus_OuterQuantifierRemoved()
    {
        return VerifyFixAsync(
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex(@"(.+)+", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [GeneratedRegex(@"(.+)", RegexOptions.None, 1000)]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedQuantifier_GroupPlusStar_OuterQuantifierRemoved()
    {
        return VerifyFixAsync(
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex(@"(a*)*", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [GeneratedRegex(@"(a*)", RegexOptions.None, 1000)]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """);
    }
}
