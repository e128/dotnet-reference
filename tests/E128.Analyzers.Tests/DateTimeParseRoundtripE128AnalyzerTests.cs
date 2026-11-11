using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeParseRoundtripE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParse_MissingDateTimeStyles_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DateTimeParseRoundtripAnalyzer>("""
                           using System;
                           using System.Globalization;
                           class C
                           {
                               void M()
                               {
                                   var d = {|E128016:DateTime.Parse("2024-01-01", CultureInfo.InvariantCulture)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParseExact_MissingDateTimeStyles_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DateTimeParseRoundtripAnalyzer>("""
                           using System;
                           using System.Globalization;
                           class C
                           {
                               void M()
                               {
                                   var d = {|E128016:DateTime.ParseExact("2024-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParse_WithDateTimeStyles_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<DateTimeParseRoundtripAnalyzer>("""
                           using System;
                           using System.Globalization;
                           class C
                           {
                               void M()
                               {
                                   var d = DateTime.Parse("2024-01-01", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                               }
                           }
                           """);
    }
}
