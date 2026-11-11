using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LargeStructByValueAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task FourLongStructParameter_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>("""
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
    public Task ThreeLongStructParameter_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>("""
            struct Medium
            {
                public long A;
                public long B;
                public long C;
            }

            class C
            {
                void M(Medium value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InModifier_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>("""
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M(in Big value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RefModifier_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>("""
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M(ref Big value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassParameter_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>("""
            class Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M(Big value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AbstractDeclaration_Fires_Override_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>("""
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            abstract class Base
            {
                public abstract void M({|E128217:Big value|});
            }

            class Derived : Base
            {
                public override void M(Big value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterfaceMember_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>("""
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            interface IHandler
            {
                void M(Big value);
            }

            class C : IHandler
            {
                public void M(Big value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullableBigStructParameter_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>("""
            struct Big
            {
                public long A;
                public long B;
                public long C;
                public long D;
            }

            class C
            {
                void M({|E128217:Big? value|})
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SmallFixedBuffer_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>("""
            unsafe struct Small
            {
                public fixed int Buffer[4];
            }

            class C
            {
                unsafe void M(Small value)
                {
                }
            }
            """,
            options: new AnalyzerTestOptions { AllowUnsafe = true });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExplicitLayoutStruct_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>("""
            using System.Runtime.InteropServices;
            [StructLayout(LayoutKind.Explicit)]
            struct Overlaid
            {
                [FieldOffset(0)]
                public long A;

                [FieldOffset(0)]
                public long B;

                [FieldOffset(0)]
                public long C;

                [FieldOffset(0)]
                public long D;
            }

            class C
            {
                void M(Overlaid value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SelfReferentialStruct_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<LargeStructByValueAnalyzer>(
            """
            struct S
            {
                public S? Next;
            }

            class C
            {
                void M(S value)
                {
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
