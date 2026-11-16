using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringConcatInLoopCodeFixBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<StringConcatInLoopAnalyzer, StringConcatInLoopCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<StringConcatInLoopAnalyzer, StringConcatInLoopCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_SkipsEarlierLocalsWithOtherInitializers()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines)
                {
                    var other = "x";
                    var result = "";
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                    }

                    return result + other;
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
                    var other = "x";
                    var result = "";
                    var sb = new StringBuilder();
                    foreach (var line in lines)
                    {
                        sb.Append(line);
                    }
                    result = sb.ToString();

                    return result + other;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesAWhileLoopAndLeavesOtherAddAssignmentsAlone()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines, int[] numbers)
                {
                    var result = "";
                    var i = 0;
                    var count = 0;
                    while (i < lines.Count)
                    {
                        {|E128067:result += lines[i]|};
                        count += 1;
                        numbers[0] += 1;
                        i++;
                    }

                    return result + count;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Text;

            class C
            {
                string M(List<string> lines, int[] numbers)
                {
                    var result = "";
                    var i = 0;
                    var count = 0;
                    var sb = new StringBuilder();
                    while (i < lines.Count)
                    {
                        sb.Append(lines[i]);
                        count += 1;
                        numbers[0] += 1;
                        i++;
                    }
                    result = sb.ToString();

                    return result + count;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PicksTheNextBuilderNameWhenTheFirstIsTaken()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                private int sb;

                string M(List<string> lines, int stringBuilder)
                {
                    var result = "";
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                    }

                    return result + stringBuilder;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Text;

            class C
            {
                private int sb;

                string M(List<string> lines, int stringBuilder)
                {
                    var result = "";
                    var builder = new StringBuilder();
                    foreach (var line in lines)
                    {
                        builder.Append(line);
                    }
                    result = builder.ToString();

                    return result + stringBuilder;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AvoidsNamesOfLocalFunctionsInTheMember()
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

                    void sb()
                    {
                    }

                    sb();
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
                    var stringBuilder = new StringBuilder();
                    foreach (var line in lines)
                    {
                        stringBuilder.Append(line);
                    }
                    result = stringBuilder.ToString();

                    void sb()
                    {
                    }

                    sb();
                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenEveryBuilderNameIsTaken()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines, int sb, int stringBuilder, int builder, int accumulator)
                {
                    var result = "";
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                    }

                    return result + sb + stringBuilder + builder + accumulator;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheSeedIsNotAStringLiteral()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class Holder
            {
                public static string Empty { get; } = "";
            }

            class C
            {
                static string Seed() => "";

                string FromProperty(List<string> lines)
                {
                    var result = Holder.Empty;
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                    }

                    return result;
                }

                string FromCall(List<string> lines)
                {
                    var result = Seed();
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
    public Task NoFix_WhenTheLocalHasNoInitializer()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines)
                {
                    string result;
                    result = "";
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
    public Task NoFix_WhenTheDeclarationHoldsSeveralVariables()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines)
                {
                    string result = "", other = "";
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                    }

                    return result + other;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheLoopLeavesTheMethod()
    {
        return VerifyNoFixAsync(
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                string Returns(List<string> lines)
                {
                    var result = "";
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                        return result;
                    }

                    return result;
                }

                string Throws(List<string> lines)
                {
                    var result = "";
                    foreach (var line in lines)
                    {
                        {|E128067:result += line|};
                        throw new InvalidOperationException();
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheLoopIsNotDirectlyInABlock()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines, bool flag)
                {
                    var result = "";
                    if (flag)
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
    public Task NoFix_WhenTheTargetIsNotALocal()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                private string _text = "";

                string[] Parts { get; } = new string[1];

                void M(List<string> lines, string[] items)
                {
                    foreach (var line in lines)
                    {
                        {|E128067:_text += line|};
                        {|E128067:items[0] += line|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheAssignmentIsPartOfALargerExpression()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines)
                {
                    var result = "";
                    var copy = "";
                    foreach (var line in lines)
                    {
                        copy = {|E128067:result += line|};
                    }

                    return result + copy;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheLocalIsAppendedFromInsideALambda()
    {
        return VerifyNoFixAsync(
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                string M(List<string> lines)
                {
                    var result = "";
                    foreach (var line in lines)
                    {
                        Action append = () => {|E128067:result += line|};
                        append();
                    }

                    return result;
                }
            }
            """);
    }
}
