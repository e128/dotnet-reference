using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SealedByDefaultAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SealedByDefaultAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassWithNonObjectBase_NoSubclass_Fires()
    {
        return VerifyAsync("""
            class Base { }
            class {|E128005:Derived|} : Base { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassWithNonObjectBase_HasSubclass_NoFire()
    {
        return VerifyAsync("""
            class Base { }
            class Middle : Base { }
            sealed class Leaf : Middle { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DirectObjectSubclass_NoFire()
    {
        return VerifyAsync("""
            class Foo { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AbstractClass_NoFire()
    {
        return VerifyAsync("""
            class Base { }
            abstract class Derived : Base { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SealedClass_NoFire()
    {
        return VerifyAsync("""
            class Base { }
            sealed class Derived : Base { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RecordClass_NoFire()
    {
        return VerifyAsync("""
            record class Base(int X);
            record class Derived(int X, int Y) : Base(X);
            """);
    }
}
