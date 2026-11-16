using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeParseRoundtripBranchCoverageTests
{
    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<DateTimeParseRoundtripAnalyzer>(
            "using System;\nusing System.Globalization;\nclass Parser { public static DateTime Parse(string text, IFormatProvider provider) => default; }\n"
            + "class C\n{\n    void M(string text)\n    {\n" + body + "\n    }\n}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForDateTimeOffsetParseAndParseExactWithoutStyles()
    {
        return VerifyAsync("""
                    var a = {|E128016:DateTimeOffset.Parse(text, CultureInfo.InvariantCulture)|};
                    var b = {|E128016:DateTimeOffset.ParseExact(text, "O", CultureInfo.InvariantCulture)|};
                    var c = {|E128016:DateTime.ParseExact(text, "O", CultureInfo.InvariantCulture)|};
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForOtherArgumentCountsAndMethodNames()
    {
        return VerifyAsync("""
                    var a = DateTime.Parse(text);
                    var b = DateTime.ParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    var c = DateTime.TryParse(text, CultureInfo.InvariantCulture, out var parsed);
                    var d = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None);
                    Local();

                    static void Local() { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAParseMethodOnAnotherType()
    {
        return VerifyAsync("""
                    var a = Parser.Parse(text, CultureInfo.InvariantCulture);
                    var b = int.Parse(text, CultureInfo.InvariantCulture);
            """);
    }
}
