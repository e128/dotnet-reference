using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ArrayPoolSqliteParameterSizeCodeFixProviderBranchCoverageGapTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheValueAssignmentUsesAComputedLength()
    {
        return CodeFixTestRunner.NoFixAsync<ArrayPoolSqliteParameterSizeAnalyzer, ArrayPoolSqliteParameterSizeCodeFixProvider>(
            """
            using System.Buffers;
            using Microsoft.Data.Sqlite;
            namespace Microsoft.Data.Sqlite
            {
                public class SqliteParameter { public object Value { get; set; } public int Size { get; set; } }
            }
            class C
            {
                void M(SqliteParameter parameter, int size)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(size * 2);
                    {|E128086:parameter.Value = buffer|};
                }
            }
            """);
    }
}
