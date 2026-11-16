using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PrimaryConstructorBackingFieldCodeFixProviderBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<PrimaryConstructorBackingFieldAnalyzer, PrimaryConstructorBackingFieldCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesAThisQualifiedFieldAccess()
    {
        return VerifyFixAsync(
            """
            class C(int value)
            {
                private readonly int {|E128017:_value|} = value;
                int Get() => this._value;
            }
            """,
            """
            class C(int value)
            {
                int Get() => value;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsOtherVariablesInTheSameFieldDeclaration()
    {
        return VerifyFixAsync(
            """
            class C(int value)
            {
                private readonly int {|E128017:_value|} = value, _other = 2;
                int Get() => _value + _other;
            }
            """,
            """
            class C(int value)
            {
                private readonly int _other = 2;
                int Get() => value + _other;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_LeavesASameNamedLocalAlone()
    {
        return VerifyFixAsync(
            """
            class C(int value)
            {
                private readonly int {|E128017:_value|} = value;
                int Get()
                {
                    int _value = 2;
                    return _value + this._value;
                }
            }
            """,
            """
            class C(int value)
            {
                int Get()
                {
                    int _value = 2;
                    return _value + value;
                }
            }
            """);
    }
}
