using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AppSettingsKeyReaderBranchCoverageTests
{
    private static Task VerifyAsync(string json, string read)
    {
        return AnalyzerTestRunner.VerifyAsync<ConfigurationKeyMissingAnalyzer>(
            $$"""
            using Microsoft.Extensions.Configuration;
            class C
            {
                string? M(IConfiguration configuration) => {{read}};
            }
            namespace Microsoft.Extensions.Configuration
            {
                public interface IConfiguration
                {
                    string? this[string key] { get; set; }
                }
            }
            """,
            options: new AnalyzerTestOptions { AdditionalFiles = [("appsettings.json", json)] });
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("-12.34e+56")]
    [InlineData("0.0E-1")]
    [InlineData("false")]
    [InlineData("\"\\b\\f\\n\\r\\t\\/\\u0041\"")]
    [InlineData("[ ]")]
    [InlineData("{ }")]
    [InlineData("1 // comment")]
    public Task ReportsMissingKey_WhenSettingsContainAValidValue(string value)
    {
        return VerifyAsync("{ \"Known\": " + value + "\n}", "{|E128195:configuration[\"Missing\"]|}");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("-")]
    [InlineData("1e")]
    [InlineData("1e+")]
    [InlineData("1eX")]
    [InlineData("1.2e-")]
    [InlineData("\"\\u12\"")]
    [InlineData("\"\\uZZZZ\"")]
    [InlineData("\"\\q\"")]
    [InlineData("[1 2]")]
    [InlineData("[1,")]
    public Task Silent_WhenSettingsAreMalformed(string value)
    {
        return VerifyAsync("{ \"Known\": " + value + " }", "configuration[\"Missing\"]");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnEscapedPropertyNameMatchesTheKey()
    {
        return VerifyAsync(
            //lang=json
            """{ "\u004bnown": 1 }""",
            "configuration[\"Known\"]");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnArrayIndexIsPresent()
    {
        return VerifyAsync(
            //lang=json
            """[ 0, { "Name": true } ]""",
            "configuration[\"1:Name\"]");
    }
}
