using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaticNumericIncrementAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<StaticNumericIncrementAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    #region Fires -- unary ++/--

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticInt_PostfixIncrement()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static int _counter;
                               void M() { _counter{|E128087:++|}; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticInt_PrefixIncrement()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static int _counter;
                               void M() { {|E128087:++|}_counter; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticInt_PostfixDecrement()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static int _counter;
                               void M() { _counter{|E128087:--|}; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticInt_PrefixDecrement()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static int _counter;
                               void M() { {|E128087:--|}_counter; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticLong_PostfixIncrement()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static long _counter;
                               void M() { _counter{|E128087:++|}; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticDouble_PostfixIncrement()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static double _counter;
                               void M() { _counter{|E128087:++|}; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticFloat_PostfixDecrement()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static float _counter;
                               void M() { _counter{|E128087:--|}; }
                           }
                           """);
    }

    #endregion Fires -- unary ++/--

    #region Fires -- compound assignments

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticInt_AddAssignment()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static int _counter;
                               void M() { _counter {|E128087:+=|} 5; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticLong_SubtractAssignment()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static long _counter;
                               void M() { _counter {|E128087:-=|} 3; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticDouble_AddAssignment()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static double _value;
                               void M() { _value {|E128087:+=|} 1.5; }
                           }
                           """);
    }

    #endregion Fires -- compound assignments

    #region Does not fire

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenStaticReadonlyInt()
    {
        // static readonly + mutation is CS0198 (compiler error), so this test
        // verifies the analyzer doesn't fire when the field is only referenced.
        return VerifyAsync("""
                           class C
                           {
                               private static readonly int _counter = 0;
                               void M() { _ = _counter; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenStaticVolatileInt()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static volatile int _counter;
                               void M() { _counter++; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenInstanceField()
    {
        return VerifyAsync("""
                           class C
                           {
                               private int _counter;
                               void M() { _counter++; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenLocalVariable()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   int counter = 0;
                                   counter++;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenLocalVariableWithAddAssignment()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   int counter = 0;
                                   counter += 1;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenParameterIncrement()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(int counter) { counter++; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenStaticStringField_IncrementNotApplicable()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static string _value;
                               void M() { _value = _value + "a"; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenStaticIntField_NotAMutationOperator()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static int _counter;
                               void M() { _counter = 42; }
                           }
                           """);
    }

    #endregion Does not fire
}
