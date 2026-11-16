using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeParseRoundtripAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheParseCallBindsDynamically()
    {
        return AnalyzerTestRunner.VerifyAsync<DateTimeParseRoundtripAnalyzer>("""
            class C
            {
                void M(string text)
                {
                    dynamic parser = new object();
                    parser.Parse(text, "format");
                    parser.ParseExact(text, "format", "provider");
                }
            }
            """);
    }
}
