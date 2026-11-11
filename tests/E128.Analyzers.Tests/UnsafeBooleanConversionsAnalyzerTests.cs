using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeBooleanConversionsAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenIntIsReadAsBool()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeBooleanConversionsAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M()
                               {
                                   int value = 1;
                                   bool flag = {|E128115:Unsafe.As<int, bool>(ref value)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenBoolIsReadAsInt()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeBooleanConversionsAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M()
                               {
                                   bool flag = true;
                                   int value = {|E128115:Unsafe.As<bool, int>(ref flag)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNeitherTypeIsBool()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeBooleanConversionsAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M()
                               {
                                   int value = 1;
                                   uint result = Unsafe.As<int, uint>(ref value);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResultIsUsedAsAReference()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeBooleanConversionsAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M()
                               {
                                   bool flag = true;
                                   ref int alias = ref Unsafe.As<bool, int>(ref flag);
                                   Unsafe.As<bool, int>(ref flag) = 0;
                               }
                           }
                           """);
    }
}
