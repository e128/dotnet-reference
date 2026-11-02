using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeParseRoundtripE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<DateTimeParseRoundtripAnalyzer, DateTimeParseRoundtripCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParse_CodeFix_AddsRoundtripKind()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Globalization;
            class C
            {
                void M()
                {
                    var d = {|E128016:DateTime.Parse("2024-01-01", CultureInfo.InvariantCulture)|};
                }
            }
            """,
            """
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

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParseExact_CodeFix_AddsRoundtripKind()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Globalization;
            class C
            {
                void M()
                {
                    var d = {|E128016:DateTime.ParseExact("2024-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture)|};
                }
            }
            """,
            """
            using System;
            using System.Globalization;
            class C
            {
                void M()
                {
                    var d = DateTime.ParseExact("2024-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
            }
            """);
    }
}
