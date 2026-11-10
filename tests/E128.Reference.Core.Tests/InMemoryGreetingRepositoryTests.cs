using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Threading;
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
        return Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GetRecentAsync_ReturnsEmpty_WhenNoGreetingsSaved()
    {
        var result = await _repository.GetRecentAsync(10, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task SaveAsync_PersistsGreeting_RetrievableViaGetRecent()
    {
        var greeting = new Greeting("Hello, World!", "World", BaseTime);

        await _repository.SaveAsync(greeting, TestContext.Current.CancellationToken);

        var result = await _repository.GetRecentAsync(10, TestContext.Current.CancellationToken);
        var single = Assert.Single(result);
        Assert.Equal(greeting, single);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GetRecentAsync_ReturnsNewestFirst()
    {
        var older = new Greeting("older", "A", BaseTime.AddMinutes(-5));
        var newer = new Greeting("newer", "B", BaseTime);

        await _repository.SaveAsync(older, TestContext.Current.CancellationToken);
        await _repository.SaveAsync(newer, TestContext.Current.CancellationToken);

        var result = await _repository.GetRecentAsync(10, TestContext.Current.CancellationToken);
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
                new Greeting("msg-" + i.ToString(CultureInfo.InvariantCulture), "X", BaseTime.AddMinutes(i)), TestContext.Current.CancellationToken);
        }

        var result = await _repository.GetRecentAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task SaveAsync_IsThreadSafe_UnderConcurrentWritersAndReaders()
    {
        const int writerCount = 4;
        const int perWriter = 500;
        var errors = new ConcurrentQueue<Exception>();

        Thread StartThread(Action body)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    body();
                }
                catch (InvalidOperationException exception)
                {
                    errors.Enqueue(exception);
                }
                catch (IndexOutOfRangeException exception)
                {
                    errors.Enqueue(exception);
                }
            });
            thread.Start();
            return thread;
        }

        var writers = Enumerable.Range(0, writerCount)
            .Select(_ => StartThread(() =>
            {
                for (var i = 0; i < perWriter; i++)
                {
                    var save = _repository.SaveAsync(new Greeting("msg", "X", BaseTime), TestContext.Current.CancellationToken);
                    Assert.True(save.IsCompletedSuccessfully);
                }
            }))
            .ToArray();

        var reader = StartThread(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                var read = _repository.GetRecentAsync(10, TestContext.Current.CancellationToken);
                Assert.True(read.IsCompletedSuccessfully);
            }
        });

        foreach (var writer in writers)
        {
            writer.Join();
        }

        reader.Join();

        Assert.Empty(errors);

        var all = await _repository.GetRecentAsync(int.MaxValue, TestContext.Current.CancellationToken);
        Assert.Equal(writerCount * perWriter, all.Count);
    }
}
