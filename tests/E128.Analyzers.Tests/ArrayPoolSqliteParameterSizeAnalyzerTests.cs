using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ArrayPoolSqliteParameterSizeAnalyzerTests
{
    private const string PooledPreamble = """
                                          using System.Buffers;
                                          using Microsoft.Data.Sqlite;
                                          namespace Microsoft.Data.Sqlite
                                          {
                                              public class SqliteParameter { public object Value { get; set; } public int Size { get; set; } }
                                              public class SqliteParameterCollection { public SqliteParameter AddWithValue(string name, object value) => new SqliteParameter(); }
                                              public class SqliteCommand { public SqliteParameterCollection Parameters { get; } = new SqliteParameterCollection(); }
                                          }
                                          """;

    private const string PlainPreamble = """
                                         using Microsoft.Data.Sqlite;
                                         namespace Microsoft.Data.Sqlite
                                         {
                                             public class SqliteParameter { public object Value { get; set; } public int Size { get; set; } }
                                             public class SqliteParameterCollection { public SqliteParameter AddWithValue(string name, object value) => new SqliteParameter(); }
                                             public class SqliteCommand { public SqliteParameterCollection Parameters { get; } = new SqliteParameterCollection(); }
                                         }
                                         """;

    /// <summary>
    ///     VB1: AddWithValue with pooled buffer, no .Size set.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenAddWithValueUsesPooledBuffer_WithoutSize()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayPoolSqliteParameterSizeAnalyzer>(PooledPreamble + """
                                            class C
                                            {
                                                void M()
                                                {
                                                    var buffer = ArrayPool<byte>.Shared.Rent(1024);
                                                    var cmd = new SqliteCommand();
                                                    _ = {|E128086:cmd.Parameters.AddWithValue("@p", buffer)|};
                                                }
                                            }
                                            """);
    }

    /// <summary>
    ///     VB2: .Value assigned pooled buffer, no .Size set.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenValueAssignedPooledBuffer_WithoutSize()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayPoolSqliteParameterSizeAnalyzer>(PooledPreamble + """
                                            class C
                                            {
                                                void M()
                                                {
                                                    var buffer = ArrayPool<byte>.Shared.Rent(1024);
                                                    var param = new SqliteParameter();
                                                    {|E128086:param.Value = buffer|};
                                                }
                                            }
                                            """);
    }

    /// <summary>
    ///     VB3: AddWithValue with pooled buffer, .Size IS set.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_NoDiag_WhenSizeIsSet()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayPoolSqliteParameterSizeAnalyzer>(PooledPreamble + """
                                            class C
                                            {
                                                void M()
                                                {
                                                    var buffer = ArrayPool<byte>.Shared.Rent(1024);
                                                    var cmd = new SqliteCommand();
                                                    var p = cmd.Parameters.AddWithValue("@p", buffer);
                                                    p.Size = 512;
                                                }
                                            }
                                            """);
    }

    /// <summary>
    ///     VB4: Buffer is new byte[], not from ArrayPool.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_NoDiag_WhenBufferIsNewByteArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayPoolSqliteParameterSizeAnalyzer>(PlainPreamble + """
                                           class C
                                           {
                                               void M()
                                               {
                                                   var buffer = new byte[1024];
                                                   var cmd = new SqliteCommand();
                                                   _ = cmd.Parameters.AddWithValue("@p", buffer);
                                               }
                                           }
                                           """);
    }

    /// <summary>
    ///     VB5: Value is string, not byte[].
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_NoDiag_WhenValueIsNotByteArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayPoolSqliteParameterSizeAnalyzer>(PlainPreamble + """
                                           class C
                                           {
                                               void M()
                                               {
                                                   var cmd = new SqliteCommand();
                                                   _ = cmd.Parameters.AddWithValue("@p", "hello");
                                               }
                                           }
                                           """);
    }
}
