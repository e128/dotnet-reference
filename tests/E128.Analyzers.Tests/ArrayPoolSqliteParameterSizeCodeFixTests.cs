using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ArrayPoolSqliteParameterSizeCodeFixTests
{
    private const string Preamble = """
                                    using System.Buffers;
                                    using Microsoft.Data.Sqlite;
                                    namespace Microsoft.Data.Sqlite
                                    {
                                        public class SqliteParameter { public object Value { get; set; } public int Size { get; set; } }
                                        public class SqliteParameterCollection { public SqliteParameter AddWithValue(string name, object value) => new SqliteParameter(); }
                                        public class SqliteCommand { public SqliteParameterCollection Parameters { get; } = new SqliteParameterCollection(); }
                                    }
                                    """;

    private static Task VerifyFixAsync(string testCode, string fixedCode)
    {
        var test = new CSharpCodeFixTest<
            ArrayPoolSqliteParameterSizeAnalyzer,
            ArrayPoolSqliteParameterSizeCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        };
        return test.RunAsync();
    }

    /// <summary>VB6: Code fix captures discard and inserts .Size for AddWithValue.</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_CapturesDiscardAndInsertsSizeForAddWithValue()
    {
        return VerifyFixAsync(
            Preamble + """
                       class C
                       {
                           void M()
                           {
                               var buffer = ArrayPool<byte>.Shared.Rent(1024);
                               var cmd = new SqliteCommand();
                               _ = {|E128086:cmd.Parameters.AddWithValue("@p", buffer)|};
                           }
                       }
                       """,
            Preamble + """
                       class C
                       {
                           void M()
                           {
                               var buffer = ArrayPool<byte>.Shared.Rent(1024);
                               var cmd = new SqliteCommand();
                               var sqliteParam = cmd.Parameters.AddWithValue("@p", buffer);
                               sqliteParam.Size = 0; // TODO: set to actual byte count
                           }
                       }
                       """);
    }

    /// <summary>VB7: Code fix inserts .Size after .Value assignment.</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_InsertsSizeAfterValueAssignment()
    {
        return VerifyFixAsync(
            Preamble + """
                       class C
                       {
                           void M()
                           {
                               var buffer = ArrayPool<byte>.Shared.Rent(1024);
                               var param = new SqliteParameter();
                               {|E128086:param.Value = buffer|};
                           }
                       }
                       """,
            Preamble + """
                       class C
                       {
                           void M()
                           {
                               var buffer = ArrayPool<byte>.Shared.Rent(1024);
                               var param = new SqliteParameter();
                               param.Value = buffer;
                               param.Size = 0; // TODO: set to actual byte count
                           }
                       }
                       """);
    }
}
