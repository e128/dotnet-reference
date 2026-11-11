using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PrimaryConstructorBackingFieldE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ReadonlyFieldFromPrimaryCtorParam_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PrimaryConstructorBackingFieldAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<PrimaryConstructorBackingFieldAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<PrimaryConstructorBackingFieldAnalyzer>("""
                           struct S(int value)
                           {
                               private readonly int _value = value;
                               int Get() => _value;
                           }
                           """);
    }
}
