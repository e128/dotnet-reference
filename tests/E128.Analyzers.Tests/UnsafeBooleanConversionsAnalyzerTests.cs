using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeBooleanConversionsAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<UnsafeBooleanConversionsAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenIntIsReadAsBool()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
