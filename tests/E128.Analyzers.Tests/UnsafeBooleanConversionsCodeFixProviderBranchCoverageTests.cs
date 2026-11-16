using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeBooleanConversionsCodeFixProviderBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<UnsafeBooleanConversionsAnalyzer, UnsafeBooleanConversionsCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReadsAMemberWithoutAddingParentheses()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                private int _value;
                bool M() => {|E128115:Unsafe.As<int, bool>(ref this._value)|};
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                private int _value;
                bool M() => (this._value != 0);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReadsAnArrayElementWithoutAddingParentheses()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                bool M(int[] values) => {|E128115:Unsafe.As<int, bool>(ref values[0])|};
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                bool M(int[] values) => (values[0] != 0);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReadsARefReturningCallWithoutAddingParentheses()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                private bool _flag;
                ref bool GetFlag() => ref _flag;
                int M() => {|E128115:Unsafe.As<bool, int>(ref GetFlag())|};
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                private bool _flag;
                ref bool GetFlag() => ref _flag;
                int M() => (GetFlag() ? 1 : 0);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ParenthesizesAConditionalRefExpression()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                bool M(bool choice, ref int first, ref int second)
                {
                    return {|E128115:Unsafe.As<int, bool>(ref (choice ? ref first : ref second))|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                bool M(bool choice, ref int first, ref int second)
                {
                    return (((choice ? ref first : ref second)) != 0);
                }
            }
            """);
    }

}
