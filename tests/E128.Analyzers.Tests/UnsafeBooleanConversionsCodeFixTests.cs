using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeBooleanConversionsCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<UnsafeBooleanConversionsAnalyzer, UnsafeBooleanConversionsCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesIntToBoolWithAComparison()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value = 1;
                    bool flag = {|E128115:Unsafe.As<int, bool>(ref value)|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value = 1;
                    bool flag = (value != 0);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesBoolToIntWithATernary()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    bool flag = true;
                    int value = {|E128115:Unsafe.As<bool, int>(ref flag)|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    bool flag = true;
                    int value = (flag ? 1 : 0);
                }
            }
            """);
    }
}
