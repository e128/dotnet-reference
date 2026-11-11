using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SealedByDefaultAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ClassWithNonObjectBase_NoSubclass_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
                           class Base { }
                           class {|E128005:Derived|} : Base { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassWithNonObjectBase_HasSubclass_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
                           class Base { }
                           class Middle : Base { }
                           sealed class Leaf : Middle { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DirectObjectSubclass_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
                           class Foo { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AbstractClass_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
                           class Base { }
                           abstract class Derived : Base { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SealedClass_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
                           class Base { }
                           sealed class Derived : Base { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RecordClass_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
                           record class Base(int X);
                           record class Derived(int X, int Y) : Base(X);
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TypeConstraint_NoFire()
    {
        // Sealing Mid would make `where T : Mid` fail with CS0701.
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
                           class Root { }
                           class Mid : Root { }
                           class Holder<T> where T : Mid { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MethodConstraint_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
                           class Root<T> { }
                           class Mid<T> : Root<T> { }
                           class Holder<T> where T : Mid<int> { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassNotUsedAsConstraint_StillFires()
    {
        return AnalyzerTestRunner.VerifyAsync<SealedByDefaultAnalyzer>("""
                           class Root { }
                           class {|E128005:Mid|} : Root { }
                           class {|E128005:Other|} : Root { }
                           """);
    }
}
