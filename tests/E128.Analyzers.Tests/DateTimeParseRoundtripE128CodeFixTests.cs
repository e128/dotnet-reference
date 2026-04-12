using System.Threading.Tasks;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeParseRoundtripE128CodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParse_CodeFix_AddsRoundtripKind()
    {
        Assert.Fail("AC4d: code fix adds DateTimeStyles.RoundtripKind to DateTime.Parse");
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParseExact_CodeFix_AddsRoundtripKind()
    {
        Assert.Fail("AC4e: code fix adds DateTimeStyles.RoundtripKind to DateTime.ParseExact");
        return Task.CompletedTask;
    }
}
