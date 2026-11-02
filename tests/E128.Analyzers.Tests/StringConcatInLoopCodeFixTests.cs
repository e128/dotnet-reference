using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringConcatInLoopCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<StringConcatInLoopAnalyzer, StringConcatInLoopCodeFixProvider>(source, fixedCode);

    private static Task VerifyNoFixAsync(string source) =>
        CodeFixTestRunner.NoFixAsync<StringConcatInLoopAnalyzer, StringConcatInLoopCodeFixProvider>(source);

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesTheLoopWithABuilder()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines)
                {
                    var result = "";
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                    }

                    return result;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Text;

            class C
            {
                string M(List<string> lines)
                {
                    var result = "";
                    var sb = new StringBuilder();
                    foreach (var line in lines)
                    {
                        sb.Append(line);
                    }
                    result = sb.ToString();

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NoFix_WhenTheValueIsReadInsideTheLoop()
    {
        // A read of the local inside the loop sees the partial string today, so the builder changes it.
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines)
                {
                    var result = "";
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                        if (result.Length > 10)
                        {
                            break;
                        }
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NoFix_WhenTheValueStartsWithText()
    {
        // The builder starts empty, so a seeded local loses its seed.
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines)
                {
                    var result = "prefix";
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                    }

                    return result;
                }
            }
            """);
    }
}
