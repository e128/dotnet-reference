using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexNestedE128CodeFixTests
{
    private const string PartialImpl = "    private static partial System.Text.RegularExpressions.Regex DigitsOnly() => null!;";

    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<GeneratedRegexAnalyzer, GeneratedRegexNestedCodeFixProvider>(source, fixedCode);

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
