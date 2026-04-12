using System.Threading.Tasks;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeParseRoundtripE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParse_MissingDateTimeStyles_Fires()
    {
        Assert.Fail("AC4a: DateTime.Parse(s, provider) without DateTimeStyles fires E128016");
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParseExact_MissingDateTimeStyles_Fires()
    {
        Assert.Fail("AC4b: DateTime.ParseExact(s, fmt, provider) without DateTimeStyles fires E128016");
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParse_WithDateTimeStyles_DoesNotFire()
    {
        Assert.Fail("AC4c: DateTime.Parse with DateTimeStyles parameter must not fire");
        return Task.CompletedTask;
    }
}
