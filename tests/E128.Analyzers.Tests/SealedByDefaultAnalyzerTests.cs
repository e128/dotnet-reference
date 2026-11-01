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
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
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

    [Fact]
    [Trait("Category", "CI")]
    public Task TypeConstraint_NoFire()
    {
        // Sealing Mid would make `where T : Mid` fail with CS0701.
        return VerifyAsync("""
                           class Root { }
                           class Mid : Root { }
                           class Holder<T> where T : Mid { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MethodConstraint_NoFire()
    {
        return VerifyAsync("""
                           class Root { }
                           class Mid : Root { }
                           static class Entry
                           {
                               internal static void Run<T>() where T : Mid { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalFunctionConstraint_NoFire()
    {
        return VerifyAsync("""
                           class Root { }
                           class Mid : Root { }
                           class Holder
                           {
                               internal static void Outer()
                               {
                                   void Inner<T>() where T : Mid { }
                                   Inner<Mid>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedTypeConstraint_NoFire()
    {
        return VerifyAsync("""
                           class Root { }
                           class Mid : Root { }
                           class Outer<T> where T : Mid
                           {
                               internal class Inner<U> where U : Mid { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComImportClass_NoFire()
    {
        // CS0424 forbids a base class on a ComImport type, so the rule never sees one.
        return VerifyAsync("""
                           using System.Runtime.InteropServices;

                           class Root { }
                           [ComImport]
                           [Guid("00000000-0000-0000-0000-000000000001")]
                           class ComServer { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GenericConstraint_NoFire()
    {
        // The constraint names a constructed type; the original definition carries the rule.
        return VerifyAsync("""
                           class Root<T> { }
                           class Mid<T> : Root<T> { }
                           class Holder<T> where T : Mid<int> { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassNotUsedAsConstraint_StillFires()
    {
        return VerifyAsync("""
                           class Root { }
                           class {|E128005:Mid|} : Root { }
                           class {|E128005:Other|} : Root { }
                           """);
    }
}
