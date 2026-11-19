using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class BlockingCallInLockAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForThreadSleep()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System.Threading;

            class C
            {
                private readonly object _gate = new();

                void M()
                {
                    lock (_gate)
                    {
                        {|E128240:Thread.Sleep(10)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAFileCall()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System.IO;

            class C
            {
                private readonly object _gate = new();

                string M(string path)
                {
                    lock (_gate)
                    {
                        return {|E128240:File.ReadAllText(path)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAStreamRead()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System.IO;

            class C
            {
                private readonly object _gate = new();

                int M(Stream stream, byte[] buffer)
                {
                    lock (_gate)
                    {
                        return {|E128240:stream.Read(buffer, 0, buffer.Length)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAStreamFlush()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System.IO;

            class C
            {
                private readonly object _gate = new();

                void M(Stream stream)
                {
                    lock (_gate)
                    {
                        {|E128240:stream.Flush()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForADbCommandExecute()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System.Data.Common;

            class C
            {
                private readonly object _gate = new();

                void M(DbCommand command)
                {
                    lock (_gate)
                    {
                        {|E128240:command.ExecuteNonQuery()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnEmbeddedStatementBody()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System.Threading;

            class C
            {
                private readonly object _gate = new();

                void M()
                {
                    lock (_gate)
                        {|E128240:Thread.Sleep(10)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForABlockingCallOutsideTheLock()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System.Threading;

            class C
            {
                private readonly object _gate = new();

                void M()
                {
                    lock (_gate)
                    {
                    }

                    Thread.Sleep(10);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAMemoryStreamRead()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System.IO;

            class C
            {
                private readonly object _gate = new();

                int M(byte[] buffer)
                {
                    var stream = new MemoryStream(buffer);
                    lock (_gate)
                    {
                        return stream.Read(buffer, 0, buffer.Length);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForALambdaBody()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System;
            using System.Threading;

            class C
            {
                private readonly object _gate = new();

                void M()
                {
                    lock (_gate)
                    {
                        Action later = () => Thread.Sleep(10);
                        later();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForALocalFunctionBody()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System.Threading;

            class C
            {
                private readonly object _gate = new();

                void M()
                {
                    lock (_gate)
                    {
                        void Later()
                        {
                            Thread.Sleep(10);
                        }

                        Later();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserTypeNamedThread()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            class Thread
            {
                public static void Sleep(int milliseconds)
                {
                }
            }

            class C
            {
                private readonly object _gate = new();

                void M()
                {
                    lock (_gate)
                    {
                        Thread.Sleep(10);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnceForANestedLock()
    {
        return AnalyzerTestRunner.VerifyAsync<BlockingCallInLockAnalyzer>("""
            using System.Threading;

            class C
            {
                private readonly object _gate = new();
                private readonly object _inner = new();

                void M()
                {
                    lock (_gate)
                    {
                        lock (_inner)
                        {
                            {|E128240:Thread.Sleep(10)|};
                        }
                    }
                }
            }
            """);
    }
}
