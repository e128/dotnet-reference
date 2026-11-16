using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers operand order, constant types, and call shapes of the month span rule.
/// </summary>
public sealed class MonthTimeSpanBranchCoverageTests
{
    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<MonthTimeSpanAnalyzer>(source, expected: null, options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSpanIsTheLeftOperandOfAnAddition()
    {
        return VerifyAsync(
            """
                           using System;
                           class C
                           {
                               static DateTime M(DateTime date, DateTimeOffset offset)
                               {
                                   var first = {|E128197:TimeSpan.FromDays(30)|} + date;
                                   var second = {|E128197:TimeSpan.FromDays(31)|} + offset;
                                   return first;
                               }
                           }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNeitherOperandIsADate()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               static TimeSpan M(TimeSpan span)
                               {
                                   span += TimeSpan.FromDays(30);
                                   return TimeSpan.FromDays(30) + TimeSpan.FromDays(30);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenASubtractionHasTheSpanOnTheLeft()
    {
        return VerifyAsync(
            """
            using System;
            class C
            {
                static object M(DateTime date)
                {
                    return TimeSpan.FromDays(30) - date;
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("27")]
    [InlineData("32")]
    [InlineData("27L")]
    [InlineData("32L")]
    [InlineData("27f")]
    [InlineData("32f")]
    [InlineData("27.0")]
    [InlineData("32.0")]
    [InlineData("30.5")]
    public Task Silent_WhenTheDayCountIsOutsideTheMonthRange(string days)
    {
        return VerifyAsync($$"""
                             using System;
                             class C
                             {
                                 static DateTime M(DateTime date)
                                 {
                                     return date.Add(TimeSpan.FromDays({{days}}));
                                 }
                             }
                             """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("28")]
    [InlineData("31")]
    [InlineData("28L")]
    [InlineData("31L")]
    [InlineData("28f")]
    [InlineData("31f")]
    [InlineData("28.0")]
    [InlineData("31.0")]
    public Task Reports_WhenTheDayCountIsAtAnEdgeOfTheMonthRange(string days)
    {
        return VerifyAsync($$"""
                             using System;
                             class C
                             {
                                 static DateTime M(DateTime date)
                                 {
                                     return date.Add({|E128197:TimeSpan.FromDays({{days}})|});
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenFromDaysHasMoreThanOneArgument()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               static DateTime M(DateTime date)
                               {
                                   return date.Add(TimeSpan.FromDays(30, 1));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSpanComesFromAnotherFactory()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               static DateTime M(DateTime date)
                               {
                                   return date.Add(TimeSpan.FromHours(720));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenFromDaysIsCalledThroughAUsingStatic()
    {
        return VerifyAsync("""
                           using System;
                           using static System.TimeSpan;
                           class C
                           {
                               static DateTime M(DateTime date)
                               {
                                   return date.Add(FromDays(30));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenFromDaysDoesNotBind()
    {
        return VerifyAsync(
            """
            using System;
            class C
            {
                static DateTime M(DateTime date)
                {
                    return date.Add(TimeSpan.FromDays(missing));
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
