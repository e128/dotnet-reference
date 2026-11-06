using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MonthTimeSpanAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<MonthTimeSpanAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CompoundAddAssignment_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTime M(DateTime date)
                               {
                                   date += {|E128197:TimeSpan.FromDays(30)|};
                                   return date;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeAddThirtyDays_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTime M(DateTime today)
                               {
                                   return today.Add({|E128197:TimeSpan.FromDays(30)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeOffsetSubtractThirtyOneDays_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTimeOffset M(DateTimeOffset now)
                               {
                                   return now.Subtract({|E128197:TimeSpan.FromDays(31)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OperatorAddTwentyEightDays_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTime M(DateTime today)
                               {
                                   return today + {|E128197:TimeSpan.FromDays(28)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OperatorSubtractTwentyNineDays_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTime M(DateTime today)
                               {
                                   return today - {|E128197:TimeSpan.FromDays(29)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoubleLiteralThirtyDays_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTime M(DateTime today)
                               {
                                   return today.Add({|E128197:TimeSpan.FromDays(30d)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoubleConstantThirtyDays_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               private const double Days = 30.0;

                               static DateTime M(DateTime today)
                               {
                                   return today.Add({|E128197:TimeSpan.FromDays(Days)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FloatConstantThirtyDays_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               private const float Days = 30f;

                               static DateTime M(DateTime today)
                               {
                                   return today.Add({|E128197:TimeSpan.FromDays(Days)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LongConstantThirtyDays_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               private const long Days = 30;

                               static DateTime M(DateTime today)
                               {
                                   return today.Add({|E128197:TimeSpan.FromDays(Days)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FractionalDays_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTime M(DateTime today)
                               {
                                   return today.Add(TimeSpan.FromDays(30.5));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SevenDaySpan_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTime M(DateTime today)
                               {
                                   return today.Add(TimeSpan.FromDays(7));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonConstantDays_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTime M(DateTime today, double days)
                               {
                                   return today.Add(TimeSpan.FromDays(days));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SpanHeldInVariable_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTime M(DateTime today)
                               {
                                   var month = TimeSpan.FromDays(30);
                                   return today.Add(month);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FromDaysOutsideDateTimeArithmetic_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static TimeSpan M()
                               {
                                   return TimeSpan.FromDays(30);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddDaysCall_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static DateTime M(DateTime today)
                               {
                                   return today.AddDays(30);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UserTypeWithFromDays_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;

                           class Span
                           {
                               internal static TimeSpan FromDays(int days)
                               {
                                   return TimeSpan.Zero;
                               }
                           }

                           class C
                           {
                               static DateTime M(DateTime today)
                               {
                                   return today.Add(Span.FromDays(30));
                               }
                           }
                           """);
    }
}
