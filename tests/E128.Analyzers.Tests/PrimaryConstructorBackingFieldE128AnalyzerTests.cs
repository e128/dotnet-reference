using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PrimaryConstructorBackingFieldE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<PrimaryConstructorBackingFieldAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadonlyFieldFromPrimaryCtorParam_Fires()
    {
        return VerifyAsync("""
            class C(int value)
            {
                private readonly int {|E128017:_value|} = value;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MutableFieldFromPrimaryCtorParam_DoesNotFire()
    {
        return VerifyAsync("""
            class C(int value)
            {
                private int _value = value;
                void Mutate() { _value = 42; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StructWithPrimaryCtorField_DoesNotFire()
    {
        return VerifyAsync("""
            struct S(int value)
            {
                private readonly int _value = value;
                int Get() => _value;
            }
            """);
    }
}
