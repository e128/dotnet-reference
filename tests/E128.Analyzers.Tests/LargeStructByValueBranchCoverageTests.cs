using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LargeStructByValueBranchCoverageTests
{
    private static readonly AnalyzerTestOptions UnsafeOptions = new() { AllowUnsafe = true };

    private static DiagnosticResult Expected(string structName, int size)
    {
        return new DiagnosticResult("E128217", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("value", structName, size);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("bool", 1)]
    [InlineData("byte", 1)]
    [InlineData("sbyte", 1)]
    [InlineData("char", 2)]
    [InlineData("short", 2)]
    [InlineData("ushort", 2)]
    [InlineData("int", 4)]
    [InlineData("uint", 4)]
    [InlineData("float", 4)]
    [InlineData("long", 8)]
    [InlineData("ulong", 8)]
    [InlineData("double", 8)]
    [InlineData("decimal", 16)]
    [InlineData("nint", 8)]
    [InlineData("nuint", 8)]
    [InlineData("string", 8)]
    [InlineData("object", 8)]
    [InlineData("int[]", 8)]
    [InlineData("int*", 8)]
    [InlineData("delegate*<void>", 8)]
    [InlineData("int?", 8)]
    [InlineData("ByteEnum", 1)]
    [InlineData("LongEnum", 8)]
    [InlineData("System.Collections.Generic.List<int>", 8)]
    public Task FieldType_AddsItsSizeToTheReportedTotal(string fieldType, int fieldSize)
    {
        var code = $$"""
            enum ByteEnum : byte { A }
            enum LongEnum : long { A }

            unsafe struct S
            {
                public long A;
                public long B;
                public long C;
                public {{fieldType}} D;
            }

            unsafe class C
            {
                void M({|#0:S value|})
                {
                }
            }
            """;

        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            code,
            Expected("S", 24 + fieldSize),
            UnsafeOptions);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LargeFixedBuffer_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            unsafe struct Large
            {
                public fixed long Buffer[4];
            }

            unsafe class C
            {
                void M({|E128217:Large value|})
                {
                }
            }
            """,
            options: UnsafeOptions);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TypeParameterFields_CountAsPointers()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            struct Quad<T>
            {
                public T A;
                public T B;
                public T C;
                public T D;
            }

            class C
            {
                void M<T>({|E128217:Quad<T> value|})
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedStructFields_AreMeasuredRecursively()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            struct Inner
            {
                public long A;
                public long B;
            }

            struct Outer
            {
                public Inner First;
                public Inner Second;
            }

            class C
            {
                void M({|E128217:Outer value|})
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InlineArrayStruct_IsMeasuredByElementCount()
    {
        const string code = """
            using System.Runtime.CompilerServices;

            [System.Obsolete]
            [InlineArray(4)]
            struct Buffer
            {
                private static int count;
                private long element;
            }

            class C
            {
                void M({|#0:Buffer value|})
                {
                }
            }
            """;

        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            code,
            Expected("Buffer", 32),
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StructWithAnUnrelatedAttribute_StillFires()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            using System;

            [Serializable]
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M({|E128217:Big value|})
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SequentialLayoutStruct_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            using System.Runtime.InteropServices;

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            struct Packed
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct Plain
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M({|E128217:Packed first|}, {|E128217:Plain second|})
                {
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LookAlikeStructLayoutAttribute_DoesNotMarkTheStructExplicit()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            namespace Other
            {
                [System.AttributeUsage(System.AttributeTargets.Struct)]
                public sealed class StructLayoutAttribute : System.Attribute
                {
                    public StructLayoutAttribute(int kind)
                    {
                    }
                }
            }

            [Other.StructLayout(2)]
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M({|E128217:Big value|})
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortStructLayoutArgument_IsNotTreatedAsExplicit()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            using System.Runtime.InteropServices;

            [StructLayout((short)2)]
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M({|E128217:Big value|})
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutParameter_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M(out Big value)
                {
                    value = default;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExtensionThisParameter_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            static class Extensions
            {
                public static long Sum(this Big value)
                {
                    return value.A;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParamsArray_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M(params Big[] values)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorParameter_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                public C({|E128217:Big value|})
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DelegateLambdaAndLocalFunctionParameters_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            using System;

            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            delegate void Handler(Big value);

            class C
            {
                void M()
                {
                    Action<Big> untyped = value => { };
                    Action<Big> typed = (Big value) => { };
                    void Local(Big value)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ImplicitInterfaceImplementation_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            interface IHandler
            {
                void Handle(Big value);
            }

            class C : IHandler
            {
                public void Handle(Big value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StructsInVectorNamespaces_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            namespace System.Numerics
            {
                public struct Wide
                {
                    public long A;
                    public long B;
                    public long C;
                    public long D;
                }
            }

            namespace System.Runtime.Intrinsics
            {
                public struct Wider
                {
                    public long A;
                    public long B;
                    public long C;
                    public long D;
                }
            }

            class C
            {
                void M(System.Numerics.Wide first, System.Runtime.Intrinsics.Wider second)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EnumAndRefStructParameters_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            enum Mode : long
            {
                A
            }

            ref struct Cursor
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M(Mode mode, (long, long, long, long) tuple, System.DateTime moment)
                {
                }

                void N(Cursor cursor)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StructChainDeeperThanTheLimit_Fires()
    {
        const int Levels = 20;
        var code = new StringBuilder();

        for (var level = 0; level < Levels; level++)
        {
            var next = level < Levels - 1 ? "S" + (level + 1).ToString(CultureInfo.InvariantCulture) : "byte";
            _ = code.Append(CultureInfo.InvariantCulture, $"struct S{level} {{ public {next} Next; }}\n");
        }

        _ = code.Append("class C { void M({|E128217:S0 value|}) { } }\n");

        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(code.ToString());
    }
}
