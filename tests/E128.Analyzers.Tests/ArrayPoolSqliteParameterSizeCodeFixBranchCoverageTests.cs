using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ArrayPoolSqliteParameterSizeCodeFixBranchCoverageTests
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

    private static Task VerifyFixAsync(string body, string fixedBody)
    {
        return CodeFixTestRunner.RunAsync<ArrayPoolSqliteParameterSizeAnalyzer, ArrayPoolSqliteParameterSizeCodeFixProvider>(
            Wrap(body),
            Wrap(fixedBody));
    }

    private static string Wrap(string body)
    {
        return Preamble + "\nclass C\n{\n    void M(bool flag, SqliteParameter parameter)\n    {\n"
               + "        var buffer = ArrayPool<byte>.Shared.Rent(16);\n        var cmd = new SqliteCommand();\n"
               + body + "\n    }\n}\n";
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PicksTheNextFreeParameterName()
    {
        return VerifyFixAsync(
            """
                    var sqliteParam = 1;
                    _ = {|E128086:cmd.Parameters.AddWithValue("@p", buffer)|};
            """,
            """
                    var sqliteParam = 1;
                    var sqliteParam2 = cmd.Parameters.AddWithValue("@p", buffer);
                    sqliteParam2.Size = 16;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_SkipsEveryTakenParameterName()
    {
        return VerifyFixAsync(
            """
                    var sqliteParam = 1;
                    var sqliteParam2 = 2;
                    _ = {|E128086:cmd.Parameters.AddWithValue("@p", buffer)|};
            """,
            """
                    var sqliteParam = 1;
                    var sqliteParam2 = 2;
                    var sqliteParam3 = cmd.Parameters.AddWithValue("@p", buffer);
                    sqliteParam3.Size = 16;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsAnAssignmentThatUsesTheResult()
    {
        return VerifyFixAsync(
            """
                    parameter = {|E128086:cmd.Parameters.AddWithValue("@p", buffer)|};
            """,
            """
                    var sqliteParam = cmd.Parameters.AddWithValue("@p", buffer);
                    sqliteParam.Size = 16;
                    parameter = sqliteParam;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheBufferIsReassigned()
    {
        return CodeFixTestRunner.NoFixAsync<ArrayPoolSqliteParameterSizeAnalyzer, ArrayPoolSqliteParameterSizeCodeFixProvider>(
            Wrap("""
                    buffer = ArrayPool<byte>.Shared.Rent(32);
                    {|E128086:parameter.Value = buffer|};
            """));
    }
}
