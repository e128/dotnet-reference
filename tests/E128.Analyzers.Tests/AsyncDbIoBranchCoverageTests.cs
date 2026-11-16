using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncDbIoBranchCoverageTests
{
    private const string Prefix = """
                                  using System;
                                  using System.Data;
                                  using System.Data.Common;
                                  using System.Threading.Tasks;

                                  sealed class FakeConnection : DbConnection
                                  {
                                      public override string ConnectionString { get; set; } = string.Empty;
                                      public override string Database => string.Empty;
                                      public override string DataSource => string.Empty;
                                      public override string ServerVersion => string.Empty;
                                      public override ConnectionState State => ConnectionState.Closed;
                                      public override void ChangeDatabase(string databaseName) { }
                                      public override void Close() { }
                                      public override void Open() { }
                                      public void Ping() { }
                                      protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
                                      protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
                                  }
                                  """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncDbIoAnalyzer>(
            Prefix + "\n" + code,
            options: new AnalyzerTestOptions { MarkupOptions = MarkupOptions.UseFirstDescriptor });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForASyncLambdaInsideAnAsyncMethod()
    {
        return VerifyAsync("""
            class C
            {
                async Task M(FakeConnection connection)
                {
                    await Task.Yield();
                    Action open = () => {|E128093:connection.Open()|};
                    open();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InAConstructorAndInSyncLocalFunctionAndAnonymousMethod()
    {
        return VerifyAsync("""
            class C
            {
                public C(FakeConnection connection)
                {
                    {|E128093:connection.Open()|};
                }

                void M(FakeConnection connection)
                {
                    void Local() => {|E128093:connection.Open()|};
                    Action anon = delegate { {|E128093:connection.Open()|}; };
                    Local();
                    anon();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_InsideAsyncLocalFunctionAndAsyncLambdas()
    {
        return VerifyAsync("""
            class C
            {
                void M(FakeConnection connection)
                {
                    async Task Local() { await Task.Yield(); connection.Open(); }
                    Func<Task> simple = async () => { await Task.Yield(); connection.Open(); };
                    Func<int, Task> single = async x => { await Task.Yield(); connection.Open(); };
                    Func<Task> anon = async delegate { await Task.Yield(); connection.Open(); };
                    _ = (Local(), simple, single, anon);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAConditionalAccessCall()
    {
        return VerifyAsync("""
            class C
            {
                void M(FakeConnection? connection)
                {
                    connection?{|E128093:.Open()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAConditionalAccessCallThatAlreadyUsesAsync()
    {
        return VerifyAsync("""
            class C
            {
                void M(FakeConnection? connection)
                {
                    _ = connection?.OpenAsync();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForNameofAndAnAlreadyAsyncCall()
    {
        return VerifyAsync("""
            class C
            {
                void M(FakeConnection connection)
                {
                    var name = nameof(connection);
                    _ = connection.OpenAsync();
                    _ = name;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAMethodWithoutAnAsyncSibling()
    {
        return VerifyAsync("""
            class C
            {
                void M(FakeConnection connection)
                {
                    connection.Ping();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForATypeNamedDbConnectionOutsideTheDataNamespace()
    {
        return VerifyAsync("""
            namespace Other
            {
                class DbConnection
                {
                    public void Open() { }
                    public Task OpenAsync() => Task.CompletedTask;
                }

                class C
                {
                    void M(DbConnection connection)
                    {
                        connection.Open();
                    }
                }
            }
            """);
    }
}
