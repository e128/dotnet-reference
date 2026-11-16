using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaticNumericIncrementCodeFixProviderBranchCoverageTests
{
    private static Microsoft.CodeAnalysis.Testing.DiagnosticResult Remaining(int line, int column, int endLine, int endColumn)
    {
        return new Microsoft.CodeAnalysis.Testing.DiagnosticResult("E128087", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithSpan(line, column, endLine, endColumn)
            .WithArguments("_value", "++");
    }

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<StaticNumericIncrementAnalyzer, StaticNumericIncrementCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesAPrefixIncrementWithInterlocked()
    {
        return VerifyFixAsync(
            """
            using System.Threading;

            class C
            {
                private static int _counter;
                void M() { {|E128087:++|}_counter; }
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
    public Task Fix_RewritesAPostfixDecrementWithInterlocked()
    {
        return VerifyFixAsync(
            """
            using System.Threading;

            class C
            {
                private static long _counter;
                void M() { _counter{|E128087:--|}; }
            }
            """,
            """
            using System.Threading;

            class C
            {
                private static long _counter;
                void M() { _ = Interlocked.Decrement(ref _counter); }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesAnAddAssignmentOfALongWithInterlockedAdd()
    {
        return VerifyFixAsync(
            """
            using System.Threading;

            class C
            {
                private static long _total;
                void M(long amount) { _total {|E128087:+=|} amount; }
            }
            """,
            """
            using System.Threading;

            class C
            {
                private static long _total;
                void M(long amount) { _ = Interlocked.Add(ref _total, amount); }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_OffersNothingInAStaticMemberOfAnInstanceClass()
    {
        return CodeFixTestRunner.RunAsync<StaticNumericIncrementAnalyzer, StaticNumericIncrementCodeFixProvider>(
            """
            class C
            {
                private static double _value;
                static void M() { _value{|E128087:++|}; }
            }
            """,
            """
            class C
            {
                private static double _value;
                static void M() { _value{|E128087:++|}; }
            }
            """.Replace("{|E128087:++|}", "++", System.StringComparison.Ordinal),
            options: new CodeFixTestOptions { FixedExpected = [Remaining(4, 29, 4, 31)] });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_OffersNothingForAFieldOfAStaticClass()
    {
        return CodeFixTestRunner.RunAsync<StaticNumericIncrementAnalyzer, StaticNumericIncrementCodeFixProvider>(
            """
            static class C
            {
                private static double _value;
                public static void M() { _value{|E128087:++|}; }
            }
            """,
            """
            static class C
            {
                private static double _value;
                public static void M() { _value++; }
            }
            """,
            options: new CodeFixTestOptions { FixedExpected = [Remaining(4, 36, 4, 38)] });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_OffersNoInterlockedFixForAnAddAssignmentInValuePosition()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static int _total;
                int M() { return _total {|E128087:+=|} 2; }
            }
            """,
            """
            class C
            {
                private int _total;
                int M() { return _total += 2; }
            }
            """);
    }
}
