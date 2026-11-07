using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EmptySpinLoopAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task EmptyWhileBody_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptySpinLoopAnalyzer>("""
            class C
            {
                private volatile bool _ready;

                void M()
                {
                    {|E128206:while|} (!_ready)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EmptyDoBody_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptySpinLoopAnalyzer>("""
            class C
            {
                private volatile bool _ready;

                void M()
                {
                    {|E128206:do|}
                    {
                    }
                    while (!_ready);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BodyWithStatement_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptySpinLoopAnalyzer>("""
            class C
            {
                private volatile bool _ready;
                private int _count;

                void M()
                {
                    while (!_ready)
                    {
                        _count++;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConditionWithInvocation_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptySpinLoopAnalyzer>("""
            using System.IO;
            class C
            {
                void M(TextReader reader)
                {
                    while (reader.Read() >= 0)
                    {
                    }
                }
            }
            """);
    }
}
