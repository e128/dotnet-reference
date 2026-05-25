using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaticNumericIncrementCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<StaticNumericIncrementAnalyzer, StaticNumericIncrementCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        return test.RunAsync();
    }

    #region Interlocked fix

    [Fact]
    [Trait("Category", "CI")]
    public Task FixInterlockedIncrement_OnPostfixIncrement()
    {
        return VerifyFixAsync(
            """
            using System.Threading;

            class C
            {
                private static int _counter;
                void M() { _counter{|E128087:++|}; }
            }
            """,
            """
            using System.Threading;

            class C
            {
                private static int _counter;
                void M() { _ = Interlocked.Increment(ref _counter); }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixInterlockedDecrement_OnPrefixDecrement()
    {
        return VerifyFixAsync(
            """
            using System.Threading;

            class C
            {
                private static int _counter;
                void M() { {|E128087:--|}_counter; }
            }
            """,
            """
            using System.Threading;

            class C
            {
                private static int _counter;
                void M() { _ = Interlocked.Decrement(ref _counter); }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixInterlockedAdd_OnAddAssignment()
    {
        return VerifyFixAsync(
            """
            using System.Threading;

            class C
            {
                private static int _counter;
                void M() { _counter {|E128087:+=|} 5; }
            }
            """,
            """
            using System.Threading;

            class C
            {
                private static int _counter;
                void M() { _ = Interlocked.Add(ref _counter, 5); }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixInterlockedAdd_Negated_OnSubtractAssignmentLong()
    {
        return VerifyFixAsync(
            """
            using System.Threading;

            class C
            {
                private static long _counter;
                void M() { _counter {|E128087:-=|} 3; }
            }
            """,
            """
            using System.Threading;

            class C
            {
                private static long _counter;
                void M() { _ = Interlocked.Add(ref _counter, -3); }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixInterlockedIncrement_AddsUsingWhenMissing()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static int _counter;
                void M() { _counter{|E128087:++|}; }
            }
            """,
            """
            using System.Threading;

            class C
            {
                private static int _counter;
                void M() { _ = Interlocked.Increment(ref _counter); }
            }
            """);
    }

    #endregion Interlocked fix

    #region Remove static fix

    [Fact]
    [Trait("Category", "CI")]
    public Task FixRemoveStatic_OnDoubleIncrement()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static double _value;
                void M() { _value{|E128087:++|}; }
            }
            """,
            """
            class C
            {
                private double _value;
                void M() { _value++; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixRemoveStatic_OnFloatDecrement()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static float _value;
                void M() { _value{|E128087:--|}; }
            }
            """,
            """
            class C
            {
                private float _value;
                void M() { _value--; }
            }
            """);
    }

    #endregion Remove static fix
}
