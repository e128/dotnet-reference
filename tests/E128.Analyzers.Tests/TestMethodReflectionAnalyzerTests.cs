using System.Threading.Tasks;
using E128.Analyzers.Maintainability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TestMethodReflectionAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TestMethodReflectionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FactMethod_BindingFlags_FiresE128090()
    {
        return VerifyAsync("""
                           using System;
                           using System.Reflection;
                           class FactAttribute : Attribute { }
                           class C
                           {
                               [Fact]
                               public void TestMethod()
                               {
                                   var flags = {|E128090:BindingFlags|}.NonPublic;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TheoryMethod_GetMethod_FiresE128090()
    {
        return VerifyAsync("""
                           using System;
                           using System.Reflection;
                           class TheoryAttribute : Attribute { }
                           class C
                           {
                               [Theory]
                               public void TestMethod()
                               {
                                   var m = {|E128090:typeof(string).GetMethod("ToString")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonTestMethod_GetMethod_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               public void Helper()
                               {
                                   var m = typeof(string).GetMethod("ToString");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FactMethod_GetProperty_FiresE128090()
    {
        return VerifyAsync("""
                           using System;
                           using System.Reflection;
                           class FactAttribute : Attribute { }
                           class C
                           {
                               [Fact]
                               public void TestMethod()
                               {
                                   var p = {|E128090:typeof(string).GetProperty("Length")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FactMethod_GetField_FiresE128090()
    {
        return VerifyAsync("""
                           using System;
                           using System.Reflection;
                           class FactAttribute : Attribute { }
                           class C
                           {
                               [Fact]
                               public void TestMethod()
                               {
                                   var f = {|E128090:typeof(string).GetField("Empty")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetupCode_BindingFlags_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Reflection;
                           class FactAttribute : Attribute { }
                           class C
                           {
                               public C()
                               {
                                   var flags = BindingFlags.NonPublic;
                               }

                               [Fact]
                               public void TestMethod()
                               {
                               }
                           }
                           """);
    }
}
