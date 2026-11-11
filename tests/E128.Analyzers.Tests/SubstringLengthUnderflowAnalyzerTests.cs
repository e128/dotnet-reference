using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SubstringLengthUnderflowAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task EarlyReturnLengthGuard_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
    public Task EarlyReturnGuardEndingInBothBranchesOfAnIf_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.Length < 2)
                                   {
                                       if (value.Length == 0)
                                       {
                                           return value;
                                       }
                                       else
                                       {
                                           return "x";
                                       }
                                   }

                                   return value.Substring(0, value.Length - 2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EarlyReturnGuardEndingInANestedBlock_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.Length < 2)
                                   {
                                       {
                                           return value;
                                       }
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
    public Task NotNullOrEmptyGuard_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (!string.IsNullOrEmpty(value))
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
    public Task NotNullOrWhiteSpaceGuard_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (!string.IsNullOrWhiteSpace(value))
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
    public Task EndsWithGuard_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.EndsWith("x"))
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
    public Task StartsWithGuard_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.StartsWith("x"))
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
    public Task ContainsGuard_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.Contains("x"))
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
    public Task IndexOfGuard_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (value.IndexOf("x") >= 0)
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
    public Task NegatedContainsGuard_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
                           class C
                           {
                               static string M(string value)
                               {
                                   if (!value.Contains("x"))
                                   {
                                       return {|E128198:value.Substring(0, value.Length - 1)|};
                                   }

                                   return value;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonEmptyGuardOnOtherString_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
                           class C
                           {
                               static string M(string value, string other)
                               {
                                   if (!string.IsNullOrEmpty(other))
                                   {
                                       return {|E128198:value.Substring(0, value.Length - 1)|};
                                   }

                                   return value;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UserTypeSubstring_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
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
