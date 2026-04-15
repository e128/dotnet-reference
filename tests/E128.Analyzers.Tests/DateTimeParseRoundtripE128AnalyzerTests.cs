using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeParseRoundtripE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<DateTimeParseRoundtripAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParse_MissingDateTimeStyles_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
