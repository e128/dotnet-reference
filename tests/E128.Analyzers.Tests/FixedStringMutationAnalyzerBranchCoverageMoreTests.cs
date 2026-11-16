using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedStringMutationAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code, bool lenient = false)
    {
        return AnalyzerTestRunner.VerifyAsync<FixedStringMutationAnalyzer>(
            code,
            options: new AnalyzerTestOptions
            {
                AllowUnsafe = true,
                CompilerDiagnostics = lenient ? CompilerDiagnostics.None : null,
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFixedPointerPointsAtAStruct()
    {
        return VerifyAsync("""
            struct Cell
            {
                public int Value;
            }

            unsafe class C
            {
                void M(Cell[] cells)
                {
                    fixed (Cell* pointer = cells)
                    {
                        pointer[0].Value = 1;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFixedDeclarationIsNotAPointerType()
    {
        return VerifyAsync(
            """
            unsafe class C
            {
                void M(string text)
                {
                    fixed (var pointer = text)
                    {
                    }
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFixedVariableHasNoInitializer()
    {
        return VerifyAsync(
            """
            unsafe class C
            {
                void M()
                {
                    fixed (char* pointer)
                    {
                    }
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInitializerHasNoType()
    {
        return VerifyAsync(
            """
            unsafe class C
            {
                void M()
                {
                    fixed (char* pointer = null)
                    {
                        pointer[0] = 'a';
                    }
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePointerIsUsedInOtherExpressionShapes()
    {
        return VerifyAsync(
            """
            unsafe class C
            {
                static void Read(char value) { }

                void M(string text)
                {
                    fixed (char* pointer = text)
                    {
                        var negated = -pointer;
                        var shifted = 1 + pointer;
                        Read(*(pointer - 1));
                    }
                }
            }
            """,
            lenient: true);
    }
}
