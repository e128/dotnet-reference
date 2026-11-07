using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnboundedChannelAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task CreateUnbounded_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UnboundedChannelAnalyzer>("""
            using System.Threading.Channels;
            class C
            {
                Channel<int> M()
                {
                    return {|E128216:Channel.CreateUnbounded<int>()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CreateUnboundedWithOptions_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UnboundedChannelAnalyzer>("""
            using System.Threading.Channels;
            class C
            {
                Channel<int> M()
                {
                    return {|E128216:Channel.CreateUnbounded<int>(new UnboundedChannelOptions())|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CreateBounded_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UnboundedChannelAnalyzer>("""
            using System.Threading.Channels;
            class C
            {
                Channel<int> M()
                {
                    return Channel.CreateBounded<int>(10);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CustomChannel_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UnboundedChannelAnalyzer>("""
            using System.Threading.Channels;
            class Channel
            {
                public static System.Threading.Channels.Channel<int> CreateUnbounded<T>()
                {
                    throw new System.NotImplementedException();
                }
            }

            class C
            {
                System.Threading.Channels.Channel<int> M()
                {
                    return Channel.CreateUnbounded<int>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CreateUnboundedPrioritized_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UnboundedChannelAnalyzer>("""
            using System.Threading.Channels;
            class C
            {
                Channel<int> M()
                {
                    return {|E128216:Channel.CreateUnboundedPrioritized<int>()|};
                }
            }
            """);
    }
}
