using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ExcessiveInheritanceE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ExcessiveInheritanceAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeLevelsDeep_Fires()
    {
        return VerifyAsync("""
            class A { }
            class B : A { }
            class C : B { }
            class {|E128046:D|} : C { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TwoLevelsDeep_NoDiagnostic()
    {
        return VerifyAsync("""
            class A { }
            class B : A { }
            class C : B { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SingleInheritance_NoDiagnostic()
    {
        return VerifyAsync("""
            class A { }
            class B : A { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FourLevelsDeep_Fires()
    {
        return VerifyAsync("""
            class A { }
            class B : A { }
            class C : B { }
            class {|E128046:D|} : C { }
            class {|E128046:E|} : D { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StructInheritance_NoDiagnostic()
    {
        // Structs cannot have user-defined base types.
        return VerifyAsync("""
            struct S { }
            """);
    }
}
