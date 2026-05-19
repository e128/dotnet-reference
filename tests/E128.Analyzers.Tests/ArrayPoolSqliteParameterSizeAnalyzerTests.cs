using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
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

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ArrayPoolSqliteParameterSizeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>VB1: AddWithValue with pooled buffer, no .Size set.</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenAddWithValueUsesPooledBuffer_WithoutSize()
    {
        return VerifyAsync(PooledPreamble + """
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

    /// <summary>VB2: .Value assigned pooled buffer, no .Size set.</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenValueAssignedPooledBuffer_WithoutSize()
    {
        return VerifyAsync(PooledPreamble + """
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

    /// <summary>VB3: AddWithValue with pooled buffer, .Size IS set.</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_NoDiag_WhenSizeIsSet()
    {
        return VerifyAsync(PooledPreamble + """
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

    /// <summary>VB4: Buffer is new byte[], not from ArrayPool.</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_NoDiag_WhenBufferIsNewByteArray()
    {
        return VerifyAsync(PlainPreamble + """
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

    /// <summary>VB5: Value is string, not byte[].</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_NoDiag_WhenValueIsNotByteArray()
    {
        return VerifyAsync(PlainPreamble + """
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
