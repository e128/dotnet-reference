using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringConcatInLoopE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<StringConcatInLoopAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringPlusEquals_InForeach_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var result = "";
                                   var items = new List<string> { "a", "b", "c" };
                                   foreach (var item in items)
                                   {
                                       {|E128067:result += item|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringPlusEquals_InForLoop_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var result = "";
                                   var items = new List<string> { "a", "b", "c" };
                                   for (int i = 0; i < items.Count; i++)
                                   {
                                       {|E128067:result += items[i]|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringPlusEquals_InWhileLoop_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   var result = "";
                                   int i = 0;
                                   while (i < 10)
                                   {
                                       {|E128067:result += "x"|};
                                       i++;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringPlusEquals_InDoWhile_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   var result = "";
                                   int i = 0;
                                   do
                                   {
                                       {|E128067:result += "x"|};
                                       i++;
                                   } while (i < 10);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringPlusEquals_OutsideLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   var result = "";
                                   result += "hello";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IntPlusEquals_InLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   int sum = 0;
                                   for (int i = 0; i < 10; i++)
                                   {
                                       sum += i;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringBuilder_InLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               void M()
                               {
                                   var sb = new StringBuilder();
                                   for (int i = 0; i < 10; i++)
                                   {
                                       sb.Append("x");
                                   }
                               }
                           }
                           """);
    }
}
