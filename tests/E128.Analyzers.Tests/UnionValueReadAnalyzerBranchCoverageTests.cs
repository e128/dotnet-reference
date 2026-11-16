using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnionValueReadAnalyzerBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<UnionValueReadAnalyzer>(
            code,
            options: new AnalyzerTestOptions
            {
                LanguageVersion = LanguageVersion.Preview,
                Sources = [UnionTestRuntime.StubSource],
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAValuePropertyOnAHandWrittenUnion()
    {
        return VerifyAsync("""
            struct Outcome : System.Runtime.CompilerServices.IUnion
            {
                public object Value => 1;
            }

            class C
            {
                object M(Outcome outcome) => {|E128221:outcome.Value|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnExplicitCastFromAUnion()
    {
        return VerifyAsync("""
            struct Outcome : System.Runtime.CompilerServices.IUnion
            {
                public static explicit operator int(Outcome value) => 1;
            }

            class C
            {
                int M(Outcome outcome) => {|E128221:(int)outcome|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnIdentityCastAndANonUnionCast()
    {
        return VerifyAsync("""
            struct Outcome : System.Runtime.CompilerServices.IUnion
            {
            }

            class C
            {
                Outcome M(Outcome outcome) => (Outcome)outcome;
                int N(long value) => (int)value;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAFieldNamedValueOnAUnion()
    {
        return VerifyAsync("""
            struct Outcome : System.Runtime.CompilerServices.IUnion
            {
                public int Value;
            }

            class C
            {
                int M(Outcome outcome) => outcome.Value;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnInheritedValueProperty()
    {
        return VerifyAsync("""
            class Base
            {
                public int Value => 1;
            }

            class Outcome : Base, System.Runtime.CompilerServices.IUnion
            {
            }

            class C
            {
                int M(Outcome outcome) => outcome.Value;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForOtherMembersOnAUnion()
    {
        return VerifyAsync("""
            struct Outcome : System.Runtime.CompilerServices.IUnion
            {
                public int Other => 1;
            }

            class C
            {
                int M(Outcome outcome) => outcome.Other;
            }
            """);
    }
}
