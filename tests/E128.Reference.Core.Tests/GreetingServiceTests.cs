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
        return Assert.ThrowsAsync<ArgumentNullException>(() => _service.GreetAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GreetAsync_ReturnsGreeting_WithDefaultName()
    {
        var result = await _service.GreetAsync(new GreetingRequest(), TestContext.Current.CancellationToken);
        var greeting = GreetingOf(result);

        Assert.Equal("Hello, World!", greeting.Message);
        Assert.Equal("World", greeting.RecipientName);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GreetAsync_ReturnsGreeting_WithCustomName()
    {
        var result = await _service.GreetAsync(new GreetingRequest("Claude"), TestContext.Current.CancellationToken);
        var greeting = GreetingOf(result);

        Assert.Equal("Hello, Claude!", greeting.Message);
        Assert.Equal("Claude", greeting.RecipientName);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GreetAsync_UsesTimeProvider_ForTimestamp()
    {
        var result = await _service.GreetAsync(new GreetingRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(FixedTime, GreetingOf(result).CreatedAt);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GreetAsync_PersistsGreeting_ToRepository()
    {
        _ = await _service.GreetAsync(new GreetingRequest("Test"), TestContext.Current.CancellationToken);

        var saved = await _repository.GetRecentAsync(1, TestContext.Current.CancellationToken);
        var single = Assert.Single(saved);
        Assert.Equal("Hello, Test!", single.Message);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GreetAsync_ReturnsFailure_WhenNameIsMissing()
    {
        var result = await _service.GreetAsync(new GreetingRequest(" "), TestContext.Current.CancellationToken);

        var error = result switch
        {
            GreetingError failure => failure,
            Greeting => null,
            null => null,
        };

        _ = Assert.IsType<GreetingError>(error);
    }

    /// <summary>
    ///     Projects the success case out of the result union. A failure case fails the test.
    /// </summary>
    /// <param name="result">The result union.</param>
    /// <returns>The greeting.</returns>
    private static Greeting GreetingOf(GreetingResult result)
    {
        var greeting = result switch
        {
            Greeting ok => ok,
            GreetingError => null,
            null => null,
        };

        return Assert.IsType<Greeting>(greeting);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
