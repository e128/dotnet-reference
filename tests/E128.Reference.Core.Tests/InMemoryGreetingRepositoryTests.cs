using System;
using System.Globalization;
using System.Threading.Tasks;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;
using Xunit;

namespace E128.Reference.Core.Tests;

public sealed class InMemoryGreetingRepositoryTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryGreetingRepository _repository = new();

    [Fact]
    [Trait("Category", "CI")]
    public Task SaveAsync_Throws_WhenGreetingIsNull()
    {
        return Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveAsync(null!));
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GetRecentAsync_ReturnsEmpty_WhenNoGreetingsSaved()
    {
        var result = await _repository.GetRecentAsync(10);

        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task SaveAsync_PersistsGreeting_RetrievableViaGetRecent()
    {
        var greeting = new Greeting("Hello, World!", "World", BaseTime);

        await _repository.SaveAsync(greeting);

        var result = await _repository.GetRecentAsync(10);
        Assert.Single(result);
        Assert.Equal(greeting, result[0]);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GetRecentAsync_ReturnsNewestFirst()
    {
        var older = new Greeting("older", "A", BaseTime.AddMinutes(-5));
        var newer = new Greeting("newer", "B", BaseTime);

        await _repository.SaveAsync(older);
        await _repository.SaveAsync(newer);

        var result = await _repository.GetRecentAsync(10);
        Assert.Equal("newer", result[0].Message);
        Assert.Equal("older", result[1].Message);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GetRecentAsync_RespectsCountLimit()
    {
        for (var i = 0; i < 5; i++)
        {
            await _repository.SaveAsync(
                new Greeting("msg-" + i.ToString(CultureInfo.InvariantCulture), "X", BaseTime.AddMinutes(i)));
        }

        var result = await _repository.GetRecentAsync(2);

        Assert.Equal(2, result.Count);
    }
}
