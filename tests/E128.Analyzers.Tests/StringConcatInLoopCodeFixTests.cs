using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringConcatInLoopCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<StringConcatInLoopAnalyzer, StringConcatInLoopCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return new CSharpCodeFixTest<StringConcatInLoopAnalyzer, StringConcatInLoopCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllInDocumentIterations = 0
        }.RunAsync();
    }

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
