using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringConcatInLoopCodeFixBranchCoverageGapTests
{
    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<StringConcatInLoopAnalyzer, StringConcatInLoopCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheSeedIsANonEmptyLiteralOrNull()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string FromText(List<string> lines)
                {
                    var result = "seed";
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                    }

                    return result;
                }

                string FromNull(List<string> lines)
                {
                    string result = null;
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheSeedIsAMemberAccessThatIsNotNamedEmpty()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class Holder
            {
                public static string Other { get; } = "";
            }

            class C
            {
                string M(List<string> lines)
                {
                    var result = Holder.Other;
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
