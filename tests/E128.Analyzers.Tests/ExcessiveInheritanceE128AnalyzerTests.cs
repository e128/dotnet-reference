using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ExcessiveInheritanceE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeLevelsDeep_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ExcessiveInheritanceAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ExcessiveInheritanceAnalyzer>("""
                           class A { }
                           class B : A { }
                           class C : B { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SingleInheritance_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<ExcessiveInheritanceAnalyzer>("""
                           class A { }
                           class B : A { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FourLevelsDeep_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ExcessiveInheritanceAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ExcessiveInheritanceAnalyzer>("""
                           struct S { }
                           """);
    }
}
