using System;
using System.Threading.Tasks;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;
using E128.Reference.Core.Services;
using Xunit;

namespace E128.Reference.Core.Tests;

public sealed class GreetingServiceTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryGreetingRepository _repository = new();
    private readonly GreetingService _service;

    public GreetingServiceTests()
    {
        var timeProvider = new FixedTimeProvider(FixedTime);
        _service = new GreetingService(new Greeter(), _repository, timeProvider);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GreetAsync_Throws_WhenRequestIsNull()
    {
        return Assert.ThrowsAsync<ArgumentNullException>(() => _service.GreetAsync(null!));
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GreetAsync_ReturnsGreeting_WithDefaultName()
    {
        var result = await _service.GreetAsync(new GreetingRequest());

        Assert.Equal("Hello, World!", result.Message);
        Assert.Equal("World", result.RecipientName);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GreetAsync_ReturnsGreeting_WithCustomName()
    {
        var result = await _service.GreetAsync(new GreetingRequest("Claude"));

        Assert.Equal("Hello, Claude!", result.Message);
        Assert.Equal("Claude", result.RecipientName);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GreetAsync_UsesTimeProvider_ForTimestamp()
    {
        var result = await _service.GreetAsync(new GreetingRequest());

        Assert.Equal(FixedTime, result.CreatedAt);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GreetAsync_PersistsGreeting_ToRepository()
    {
        await _service.GreetAsync(new GreetingRequest("Test"));

        var saved = await _repository.GetRecentAsync(1);
        Assert.Single(saved);
        Assert.Equal("Hello, Test!", saved[0].Message);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
