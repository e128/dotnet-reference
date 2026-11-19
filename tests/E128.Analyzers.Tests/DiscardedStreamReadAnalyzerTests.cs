using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DiscardedStreamReadAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBufferIsReadLater()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class C
            {
                void M(System.IO.Stream stream, byte[] buffer)
                {
                    {|E128239:stream.Read(buffer, 0, buffer.Length)|};
                    System.Text.Encoding.UTF8.GetString(buffer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForTheSpanOverload()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            using System;

            class C
            {
                void M(System.IO.Stream stream, byte[] buffer)
                {
                    {|E128239:stream.Read(buffer.AsSpan())|};
                    System.Text.Encoding.UTF8.GetString(buffer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFieldBufferIsReadLater()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class C
            {
                private readonly byte[] _buffer = new byte[16];

                void M(System.IO.Stream stream)
                {
                    {|E128239:stream.Read(_buffer, 0, _buffer.Length)|};
                    System.Text.Encoding.UTF8.GetString(_buffer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyTheLaterPrefixIsRead()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class C
            {
                void M(System.IO.Stream stream, byte[] buffer)
                {
                    {|E128239:stream.Read(buffer, 0, buffer.Length)|};
                    System.Text.Encoding.UTF8.GetString(buffer, 0, 4);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheByteCountIsStored()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class C
            {
                void M(System.IO.Stream stream, byte[] buffer)
                {
                    var read = stream.Read(buffer, 0, buffer.Length);
                    System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBufferIsNotReadLater()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class C
            {
                void M(System.IO.Stream stream, byte[] buffer)
                {
                    stream.Read(buffer, 0, buffer.Length);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBufferIsReassignedBeforeTheRead()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class C
            {
                void M(System.IO.Stream stream, byte[] buffer)
                {
                    stream.Read(buffer, 0, buffer.Length);
                    buffer = new byte[4];
                    System.Text.Encoding.UTF8.GetString(buffer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenADifferentBufferIsReadLater()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class C
            {
                void M(System.IO.Stream stream, byte[] buffer, byte[] other)
                {
                    stream.Read(buffer, 0, buffer.Length);
                    System.Text.Encoding.UTF8.GetString(other);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserTypeWithItsOwnRead()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class Reader
            {
                public int Read(byte[] buffer, int offset, int count) => 0;
            }

            class C
            {
                void M(Reader reader, byte[] buffer)
                {
                    reader.Read(buffer, 0, buffer.Length);
                    System.Text.Encoding.UTF8.GetString(buffer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForTheAsyncMember()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class C
            {
                void M(System.IO.Stream stream, byte[] buffer)
                {
                    stream.ReadAsync(buffer, 0, buffer.Length);
                    System.Text.Encoding.UTF8.GetString(buffer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLaterReadSitsInsideALambda()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class C
            {
                void M(System.IO.Stream stream, byte[] buffer)
                {
                    stream.Read(buffer, 0, buffer.Length);
                    System.Action use = () => System.Text.Encoding.UTF8.GetString(buffer);
                    use();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLaterReadSitsInsideALocalFunction()
    {
        return AnalyzerTestRunner.VerifyAsync<DiscardedStreamReadAnalyzer>("""
            class C
            {
                void M(System.IO.Stream stream, byte[] buffer)
                {
                    stream.Read(buffer, 0, buffer.Length);

                    void Use()
                    {
                        System.Text.Encoding.UTF8.GetString(buffer);
                    }

                    Use();
                }
            }
            """);
    }
}
