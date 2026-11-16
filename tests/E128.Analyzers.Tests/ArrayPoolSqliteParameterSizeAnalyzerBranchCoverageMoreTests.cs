using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ArrayPoolSqliteParameterSizeAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyLenientAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayPoolSqliteParameterSizeAnalyzer>(
            code,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAddWithValueCallDoesNotResolve()
    {
        return VerifyLenientAsync("""
            using System.Buffers;
            class C
            {
                void M(Missing cmd)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    cmd.AddWithValue("@p", buffer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueTargetIsAField()
    {
        return VerifyLenientAsync("""
            using System.Buffers;
            class FieldHolder { public object Value; }
            class C
            {
                void M(FieldHolder holder)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    holder.Value = buffer;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBufferInitializerCallDoesNotResolve()
    {
        return VerifyLenientAsync("""
            using Microsoft.Data.Sqlite;
            namespace Microsoft.Data.Sqlite
            {
                public class SqliteParameterCollection
                {
                    public void AddWithValue(string name, object value) { }
                }
            }
            class C
            {
                void M(SqliteParameterCollection parameters)
                {
                    byte[] buffer = Missing.Rent(8);
                    parameters.AddWithValue("@p", buffer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheResultIsAssignedToAMemberInsteadOfALocal()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayPoolSqliteParameterSizeAnalyzer>(
            """
            using System.Buffers;
            class SqliteParameter { public object Value { get; set; } public int Size { get; set; } }
            class SqliteParameterCollection { public SqliteParameter AddWithValue(string name, object value) => new SqliteParameter(); }
            class Holder { public SqliteParameter Slot; }
            class C
            {
                void M(SqliteParameterCollection parameters, Holder holder)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    holder.Slot = {|E128086:parameters.AddWithValue("@p", buffer)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStatementSitsOutsideAnyBlock()
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayPoolSqliteParameterSizeAnalyzer>(
            """
            using System.Buffers;
            var parameters = new SqliteParameterCollection();
            var buffer = ArrayPool<byte>.Shared.Rent(8);
            var parameter = {|E128086:parameters.AddWithValue("@p", buffer)|};
            parameter.Size = 8;

            class SqliteParameter { public object Value { get; set; } public int Size { get; set; } }
            class SqliteParameterCollection { public SqliteParameter AddWithValue(string name, object value) => new SqliteParameter(); }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
