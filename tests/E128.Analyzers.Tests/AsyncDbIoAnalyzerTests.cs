using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncDbIoAnalyzerTests
{
    private const string FakeConnectionSource = """
                                                using System;
                                                using System.Data;
                                                using System.Data.Common;

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

    private const string FakeConnectionSourceWithTaskUsing = """
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

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return VerifyAsync(FakeConnectionSource, code, expected);
    }

    private static Task VerifyAsync(string prefix, string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<AsyncDbIoAnalyzer, DefaultVerifier>
        {
            TestCode = prefix + "\n" + code,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>
    ///     AC-1: E128093 fires on a sync DbConnection call inside a fully synchronous method, resolving the async sibling
    ///     declared on the DbConnection base type.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncDbIoAnalyzer_Fires_OnSyncDbConnectionCallInSynchronousMethod()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(FakeConnection connection)
                               {
                                   {|E128093:connection.Open()|};
                               }
                           }
                           """);
    }

    /// <summary>AC-2: E128093 does not fire when the containing method is already async.</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncDbIoAnalyzer_DoesNotFire_WhenContainingMethodAlreadyAsync()
    {
        return VerifyAsync(FakeConnectionSourceWithTaskUsing, """
                                                              class C
                                                              {
                                                                  async Task M(FakeConnection connection)
                                                                  {
                                                                      connection.Open();
                                                                      await Task.CompletedTask;
                                                                  }
                                                              }
                                                              """);
    }

    /// <summary>AC-3: E128093 does not fire on a DbConnection-derived member with no Async sibling.</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncDbIoAnalyzer_DoesNotFire_OnMemberWithoutAsyncSibling()
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
}
