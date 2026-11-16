using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AppSettingsKeyReaderBranchCoverageMoreTests
{
    private static string Member(string value)
    {
        return "{ \"Known\": " + value + " }";
    }

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

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnObjectMemberLacksACommaSeparator()
    {
        return VerifyAsync(Member("1 \"Other\": 2"), "configuration[\"Missing\"]");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAStringHoldsARawControlCharacter()
    {
        return VerifyAsync(Member("\"a\tb\""), "configuration[\"Missing\"]");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTextEndsAfterABackslash()
    {
        return VerifyAsync("{ \"Known\": \"\\", "configuration[\"Missing\"]");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAUnicodeEscapeRunsPastTheEndOfTheText()
    {
        return VerifyAsync("{ \"Known\": \"\\u12", "configuration[\"Missing\"]");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALiteralRunsToTheEndOfTheText()
    {
        return VerifyAsync("[ 1", "configuration[\"Missing\"]");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReportsMissingKey_WhenAFractionEndsTheNumberToken()
    {
        return VerifyAsync(Member("1.5"), "{|E128195:configuration[\"Missing\"]|}");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReportsMissingKey_WhenALineCommentEndsTheText()
    {
        return VerifyAsync(Member("1") + " // trailing", "{|E128195:configuration[\"Missing\"]|}");
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Silent_WhenAStringEscapeIsUnknown()
    {
        foreach (var escape in new[] { "\\!", "\\0", "\\a", "\\z", "\\~" })
        {
            await VerifyAsync(Member("\"" + escape + "\""), "configuration[\"Missing\"]");
        }
    }
}
