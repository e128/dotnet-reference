using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StackAllocSafetyBranchCoverageTests
{
    private static Task VerifyAsync(string body, bool allowUnsafe = false)
    {
        return AnalyzerTestRunner.VerifyAsync<StackAllocSafetyAnalyzer>(
            "using System;\nclass C\n{\n    void M(int length, int count, bool flag)\n    {\n" + body + "\n    }\n}\n",
            options: new AnalyzerTestOptions { AllowUnsafe = allowUnsafe });
    }

    private static Task VerifyNoFixAsync(string code)
    {
        return CodeFixTestRunner.RunAsync<StackAllocSafetyAnalyzer, StackAllocSafetyCodeFixProvider>(
            code,
            code,
            options: new CodeFixTestOptions { AllowUnsafe = true });
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("foreach (var item in new[] { 1 }) { Span<byte> buffer = {|E128109:stackalloc byte[16]|}; }")]
    [InlineData("while (flag) { Span<byte> buffer = {|E128109:stackalloc byte[16]|}; }")]
    [InlineData("do { Span<byte> buffer = {|E128109:stackalloc byte[16]|}; } while (flag);")]
    public Task Reports_InsideEveryLoopKind(string body)
    {
        return VerifyAsync(body);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForALambdaInsideALoopWithASmallBuffer()
    {
        return VerifyAsync("""
                    for (var i = 0; i < count; i++)
                    {
                        Action action = () => { Span<byte> buffer = stackalloc byte[16]; };
                        action();
                    }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("int", "257", "1028")]
    [InlineData("char", "513", "1026")]
    [InlineData("long", "129", "1032")]
    [InlineData("byte", "1025", "1025")]
    public Task Reports_WhenTheConstantSizeInBytesExceedsTheLimit(string element, string count, string bytes)
    {
        return AnalyzerTestRunner.VerifyAsync<StackAllocSafetyAnalyzer>(
            $$"""
            using System;
            class C
            {
                void M()
                {
                    Span<{{element}}> buffer = {|#0:stackalloc {{element}}[{{count}}]|};
                }
            }
            """,
            new DiagnosticResult(StackAllocSafetyAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments($"its size is {bytes} bytes, above the 1024 byte limit"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnInitializerStackAllocWithoutALength()
    {
        return VerifyAsync("""
                    Span<int> sized = stackalloc int[] { 1, 2, 3 };
                    Span<int> implicitly = stackalloc[] { 1, 2, 3 };
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAConditionalExpressionGuardsTheLength()
    {
        return VerifyAsync("""
                    Span<byte> buffer = length <= 256 ? stackalloc byte[length] : new byte[length];
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardSitsOutsideTheLambda()
    {
        return VerifyAsync("""
                    if (length <= 256)
                    {
                        Action action = () => { Span<byte> buffer = {|E128109:stackalloc byte[length]|}; };
                        action();
                    }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnIfStatementGuardsAnExpressionOverTheLength()
    {
        return VerifyAsync("""
                    if (length * 2 <= 256)
                    {
                        Span<byte> buffer = stackalloc byte[length * 2];
                    }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheDeclarationHoldsTwoPointers()
    {
        return VerifyNoFixAsync("""
            unsafe class C
            {
                void M()
                {
                    int* first = {|E128109:stackalloc int[4]|}, second = null;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenThePointerIsUsedBeyondElementAccess()
    {
        return VerifyNoFixAsync("""
            unsafe class C
            {
                static void Use(int* pointer) { }

                void M()
                {
                    int* buffer = {|E128109:stackalloc int[4]|};
                    Use(buffer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAnotherNameUsesElementAccessOnADifferentVariable()
    {
        return VerifyNoFixAsync("""
            unsafe class C
            {
                void M(int* other)
                {
                    int* buffer = {|E128109:stackalloc int[4]|};
                    other[0] = buffer[1] + (buffer + 1)[0];
                }
            }
            """);
    }
}
