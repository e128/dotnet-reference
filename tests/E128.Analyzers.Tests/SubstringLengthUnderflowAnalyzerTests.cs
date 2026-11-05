using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SubstringLengthUnderflowAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SubstringLengthUnderflowAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EarlyReturnLengthGuard_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.Length < 2)
                                   {
                                       return value;
                                   }

                                   return value.Substring(0, value.Length - 2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LengthGuardThatDoesNotExit_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.Length < 2)
                                   {
                                       System.Console.WriteLine(value);
                                   }

                                   return {|E128198:value.Substring(0, value.Length - 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ElseBranchOfLongerThanTest_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.Length > 10)
                                   {
                                       return value;
                                   }
                                   else
                                   {
                                       return {|E128198:value.Substring(0, value.Length - 2)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThenBranchOfLongerThanTest_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.Length > 10)
                                   {
                                       return value.Substring(0, value.Length - 2);
                                   }

                                   return value;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ElseBranchOfShorterThanTest_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.Length < 3)
                                   {
                                       return value;
                                   }
                                   else
                                   {
                                       return value.Substring(0, value.Length - 2);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LengthMinusOne_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value)
                               {
                                   return {|E128198:value.Substring(0, value.Length - 1)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LengthMinusStartMinusOne_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value, int start)
                               {
                                   return {|E128198:value.Substring(start, value.Length - start - 1)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantFieldReceiver_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static readonly string Value = "text";

                               static string M()
                               {
                                   return {|E128198:Value.Substring(0, Value.Length - 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LengthGuardInEnclosingIf_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.Length > 1)
                                   {
                                       return value.Substring(0, value.Length - 1);
                                   }

                                   return value;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LengthGuardInConditional_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value)
                               {
                                   return value.Length > 1 ? value.Substring(0, value.Length - 1) : value;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuardOnOtherString_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value, string other)
                               {
                                   if (value.Length > 1)
                                   {
                                       return {|E128198:other.Substring(0, other.Length - 1)|};
                                   }

                                   return value;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LengthMinusVariable_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value, int start)
                               {
                                   return value.Substring(start, value.Length - start);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantCount_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value)
                               {
                                   return value.Substring(0, 3);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OneArgumentOverload_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string M(string value)
                               {
                                   return value.Substring(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UserTypeSubstring_DoesNotFire()
    {
        return VerifyAsync("""
                           class Text
                           {
                               internal int Length => 0;

                               internal string Substring(int start, int length)
                               {
                                   return string.Empty;
                               }
                           }

                           class C
                           {
                               static string M(Text value)
                               {
                                   return value.Substring(0, value.Length - 1);
                               }
                           }
                           """);
    }
}
