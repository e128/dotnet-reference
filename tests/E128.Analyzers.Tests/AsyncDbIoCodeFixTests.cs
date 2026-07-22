using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncDbIoCodeFixTests
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
                                                                 protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
                                                                 protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
                                                             }
                                                             """;

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<AsyncDbIoAnalyzer, AsyncDbIoCodeFixProvider, DefaultVerifier>
        {
            TestCode = FakeConnectionSource + "\n" + source,
            FixedCode = FakeConnectionSourceWithTaskUsing + "\n" + fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    /// <summary>AC-4: fixing a void sync method promotes it to async Task and awaits the Async sibling.</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncDbIoCodeFix_PromotesVoidMethodToAsyncTask_WhenFixApplied()
    {
        const string source = """
                              class C
                              {
                                  void M(FakeConnection connection)
                                  {
                                      {|E128093:connection.Open()|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     async Task M(FakeConnection connection)
                                     {
                                         await connection.OpenAsync();
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
