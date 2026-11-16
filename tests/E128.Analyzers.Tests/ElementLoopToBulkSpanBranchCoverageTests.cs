using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ElementLoopToBulkSpanBranchCoverageTests
{
    private static DiagnosticResult Expected(string method)
    {
        return new DiagnosticResult("E128140", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments(method);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("byte", "0", "Clear")]
    [InlineData("byte", "7", "Fill")]
    [InlineData("sbyte", "0", "Clear")]
    [InlineData("sbyte", "-3", "Fill")]
    [InlineData("short", "0", "Clear")]
    [InlineData("short", "5", "Fill")]
    [InlineData("ushort", "0", "Clear")]
    [InlineData("ushort", "5", "Fill")]
    [InlineData("int", "0", "Clear")]
    [InlineData("int", "5", "Fill")]
    [InlineData("uint", "0", "Clear")]
    [InlineData("uint", "5", "Fill")]
    [InlineData("long", "0", "Clear")]
    [InlineData("long", "5", "Fill")]
    [InlineData("ulong", "0", "Clear")]
    [InlineData("ulong", "5", "Fill")]
    [InlineData("char", "'\\0'", "Clear")]
    [InlineData("char", "'a'", "Fill")]
    [InlineData("float", "0f", "Clear")]
    [InlineData("float", "1.5f", "Fill")]
    [InlineData("double", "0d", "Clear")]
    [InlineData("double", "1.5d", "Fill")]
    [InlineData("decimal", "0m", "Clear")]
    [InlineData("decimal", "1.5m", "Fill")]
    [InlineData("bool", "false", "Clear")]
    [InlineData("bool", "true", "Fill")]
    public Task ConstantFillValue_ClearsOnlyWhenTheConstantIsZero(string elementType, string constant, string method)
    {
        var code = $$"""
            class C
            {
                const {{elementType}} Value = {{constant}};

                void M({{elementType}}[] values)
                {
                    {|#0:for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = Value;
                    }|}
                }
            }
            """;

        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(code, Expected(method));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullLiteral_Clears_WhenTheElementCanHoldNull()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            class C
            {
                void M(string[] names, int?[] numbers)
                {
                    {|#0:for (var i = 0; i < names.Length; i++)
                    {
                        names[i] = null;
                    }|}

                    {|#1:for (var i = 0; i < numbers.Length; i++)
                    {
                        numbers[i] = null;
                    }|}
                }
            }
            """,
            Expected("Clear"),
            new DiagnosticResult("E128140", DiagnosticSeverity.Warning).WithLocation(1).WithArguments("Clear"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DefaultValues_Clear()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            class C
            {
                void M(int[] literal, int[] expression)
                {
                    {|#0:for (var i = 0; i < literal.Length; i++)
                    {
                        literal[i] = default;
                    }|}

                    {|#1:for (var i = 0; i < expression.Length; i++)
                    {
                        expression[i] = default(int);
                    }|}
                }
            }
            """,
            Expected("Clear"),
            new DiagnosticResult("E128140", DiagnosticSeverity.Warning).WithLocation(1).WithArguments("Clear"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task VariableValues_Fill_WhenTheyAreLocalsParametersOrFields()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            class C
            {
                int field;

                void M(int[] values, int parameter)
                {
                    var local = 3;

                    {|#0:for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = local;
                    }|}

                    {|#1:for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = parameter;
                    }|}

                    {|#2:for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = this.field;
                    }|}
                }
            }
            """,
            [
                Expected("Fill"),
                new DiagnosticResult("E128140", DiagnosticSeverity.Warning).WithLocation(1).WithArguments("Fill"),
                new DiagnosticResult("E128140", DiagnosticSeverity.Warning).WithLocation(2).WithArguments("Fill"),
            ]);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonVariableValues_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            class C
            {
                int Property { get; } = 1;

                void M(int[] values, C other)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = Property;
                    }

                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = this.Property;
                    }

                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = other.Property;
                    }

                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = 1 + 1;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReferenceAndManagedElements_Fill()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            struct Pair
            {
                public string Name;
            }

            class C
            {
                void M(string[] names, Pair[] pairs, string name, Pair pair)
                {
                    {|#0:for (var i = 0; i < names.Length; i++)
                    {
                        names[i] = name;
                    }|}

                    {|#1:for (var i = 0; i < pairs.Length; i++)
                    {
                        pairs[i] = pair;
                    }|}
                }
            }
            """,
            Expected("Fill"),
            new DiagnosticResult("E128140", DiagnosticSeverity.Warning).WithLocation(1).WithArguments("Fill"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UserDefinedConversionFromZero_Fill()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            struct Wrapper
            {
                public int Value;

                public static implicit operator Wrapper(int value)
                {
                    return new Wrapper { Value = value + 1 };
                }
            }

            class C
            {
                const int Zero = 0;

                void M(Wrapper[] values)
                {
                    {|#0:for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = Zero;
                    }|}
                }
            }
            """,
            Expected("Fill"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SpanDestination_Fills()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            using System;

            class C
            {
                void M(Span<int> values, int seed)
                {
                    {|#0:for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = seed;
                    }|}
                }
            }
            """,
            Expected("Fill"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CopyFromASpan_Reports()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            using System;

            class C
            {
                void M(Span<int> source, int[] destination, Span<int> spanDestination)
                {
                    {|#0:for (var i = 0; i < source.Length; i++)
                    {
                        destination[i] = source[i];
                    }|}

                    {|#1:for (var i = 0; i < source.Length; i++)
                    {
                        spanDestination[i] = source[i];
                    }|}
                }
            }
            """,
            Expected("CopyTo"),
            new DiagnosticResult("E128140", DiagnosticSeverity.Warning).WithLocation(1).WithArguments("CopyTo"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CopyThatDoesNotMatchTheSourceLength_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            class C
            {
                void M(int[] source, int[] destination, long[] wide, string[] names)
                {
                    for (var i = 0; i < destination.Length; i++)
                    {
                        destination[i] = source[i];
                    }

                    for (var i = 0; i < source.Length; i++)
                    {
                        wide[i] = source[i];
                    }

                    for (var i = 0; i < names.Length; i++)
                    {
                        destination[i] = source[i];
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoopBodiesThatAreNotASingleAssignment_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            class C
            {
                void M(int[] values, int count)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = 1;
                        count++;
                    }

                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] += 1;
                    }

                    for (var i = 0; i < values.Length; i++)
                    {
                        count++;
                    }

                    for (var i = 0; i < values.Length; i++)
                    {
                        if (count > 0)
                        {
                            values[i] = 1;
                        }
                    }

                    for (var i = 0; i < values.Length; i++)
                    {
                    }

                    for (var i = 0; i < count; i++)
                    {
                        values[i] = 1;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DestinationThatIsNotAnArrayOrSpan_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            class Grid
            {
                public int Length { get; }

                public int this[int index]
                {
                    get => 0;
                    set { }
                }
            }

            class C
            {
                void M(Grid grid, int[,] matrix)
                {
                    for (var i = 0; i < grid.Length; i++)
                    {
                        grid[i] = 1;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnbracedLoopBody_Reports()
    {
        return AnalyzerTestRunner.VerifyAsync<ElementLoopToBulkSpanAnalyzer>(
            """
            class C
            {
                void M(int[] values)
                {
                    {|#0:for (var i = 0; i < values.Length; i++)
                        values[i] = 1;|}
                }
            }
            """,
            Expected("Fill"));
    }
}
