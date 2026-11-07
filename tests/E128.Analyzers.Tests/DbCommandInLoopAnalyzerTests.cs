using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DbCommandInLoopAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ExecuteNonQueryAsync_InForeach_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DbCommandInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Data.Common;
            using System.Threading.Tasks;
            class C
            {
                async Task M(DbCommand command, IReadOnlyList<int> ids)
                {
                    foreach (int id in ids)
                    {
                        await {|E128204:command.ExecuteNonQueryAsync()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExecuteScalarAsync_InWhileLoop_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DbCommandInLoopAnalyzer>("""
            using System.Data.Common;
            using System.Threading.Tasks;
            class C
            {
                async Task M(DbCommand command, int count)
                {
                    while (count > 0)
                    {
                        await {|E128204:command.ExecuteScalarAsync()|};
                        count--;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExecuteReaderAsync_OutsideLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<DbCommandInLoopAnalyzer>("""
            using System.Data.Common;
            using System.Threading.Tasks;
            class C
            {
                async Task M(DbCommand command)
                {
                    await command.ExecuteReaderAsync();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SyncExecuteNonQuery_InLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<DbCommandInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Data.Common;
            class C
            {
                void M(DbCommand command, IReadOnlyList<int> ids)
                {
                    foreach (int id in ids)
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExecuteNonQueryAsync_OnUnrelatedType_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<DbCommandInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Threading.Tasks;
            class Command
            {
                public Task ExecuteNonQueryAsync() => Task.CompletedTask;
            }

            class C
            {
                async Task M(Command command, IReadOnlyList<int> ids)
                {
                    foreach (int id in ids)
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExecuteReaderAsync_InLoop_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DbCommandInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Data.Common;
            using System.Threading.Tasks;
            class C
            {
                async Task M(DbCommand command, IReadOnlyList<int> ids)
                {
                    foreach (int id in ids)
                    {
                        await {|E128204:command.ExecuteReaderAsync()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoopWithDelay_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<DbCommandInLoopAnalyzer>("""
            using System.Data.Common;
            using System.Threading.Tasks;
            class C
            {
                async Task M(DbCommand command)
                {
                    while (true)
                    {
                        await Task.Delay(100);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CallInsideLocalFunction_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<DbCommandInLoopAnalyzer>("""
            using System.Data.Common;
            using System.Threading.Tasks;
            class C
            {
                async Task M(DbCommand command, bool more)
                {
                    while (more)
                    {
                        void Local()
                        {
                            command.ExecuteNonQueryAsync();
                        }

                        await Task.Yield();
                        Local();
                    }
                }
            }
            """);
    }
}
