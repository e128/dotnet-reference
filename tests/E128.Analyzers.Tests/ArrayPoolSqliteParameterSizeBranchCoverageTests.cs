using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ArrayPoolSqliteParameterSizeBranchCoverageTests
{
    private const string Preamble = """
                                    using System;
                                    using System.Buffers;
                                    using Microsoft.Data.Sqlite;
                                    namespace Microsoft.Data.Sqlite
                                    {
                                        public class SqliteParameter { public object Value { get; set; } public int Size { get; set; } }
                                        public class SqliteParameterCollection
                                        {
                                            public SqliteParameter AddWithValue(string name, object value) => new SqliteParameter();
                                            public SqliteParameter AddWithValue(string name, object value, int size) => new SqliteParameter();
                                        }
                                        public class SqliteCommand { public SqliteParameterCollection Parameters { get; } = new SqliteParameterCollection(); }
                                    }
                                    class OtherCollection { public void AddWithValue(string name, object value) { } }
                                    class Holder { public object Value { get; set; } public object Field; }
                                    class Pool<T> { public static T[] Rent(int size) => new T[size]; }
                                    class Plain { public static byte[] Rent(int size) => new byte[size]; }
                                    """;

    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<ArrayPoolSqliteParameterSizeAnalyzer>(
            Preamble + "\nclass C\n{\n    void M(byte[] argument)\n    {\n        var cmd = new SqliteCommand();\n" + body + "\n    }\n}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForTheThreeArgumentAddWithValueOverload()
    {
        return VerifyAsync("""
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    cmd.Parameters.AddWithValue("@p", buffer, 4);
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnAddWithValueOnAnotherCollectionType()
    {
        return VerifyAsync("""
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    new OtherCollection().AddWithValue("@p", buffer);
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueIsNotALocalBuffer()
    {
        return VerifyAsync("""
                    cmd.Parameters.AddWithValue("@p", ArrayPool<byte>.Shared.Rent(8));
                    cmd.Parameters.AddWithValue("@p", argument);
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalDoesNotComeFromArrayPoolRent()
    {
        return VerifyAsync("""
                    byte[] declared;
                    declared = ArrayPool<byte>.Shared.Rent(8);
                    var viaAlias = argument;
                    var viaPool = Pool<byte>.Rent(8);
                    var viaPlain = Plain.Rent(8);
                    var viaShared = ArrayPool<byte>.Shared.Rent(8).AsSpan().ToArray();
                    cmd.Parameters.AddWithValue("@a", declared);
                    cmd.Parameters.AddWithValue("@b", viaAlias);
                    cmd.Parameters.AddWithValue("@c", viaPool);
                    cmd.Parameters.AddWithValue("@d", viaPlain);
                    cmd.Parameters.AddWithValue("@e", viaShared);
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheResultIsAssignedAndNoSizeFollows()
    {
        return VerifyAsync("""
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    SqliteParameter parameter;
                    parameter = {|E128086:cmd.Parameters.AddWithValue("@p", buffer)|};
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAssignedResultReceivesASizeLater()
    {
        return VerifyAsync("""
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    SqliteParameter parameter;
                    parameter = cmd.Parameters.AddWithValue("@p", buffer);
                    parameter.Size = 8;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSizeIsSetBeforeTheCallOrOnAnotherParameter()
    {
        return VerifyAsync("""
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    var other = new SqliteParameter();
                    var parameter = new SqliteParameter();
                    parameter.Size = 8;
                    var added = {|E128086:cmd.Parameters.AddWithValue("@p", buffer)|};
                    other.Size = 8;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValuePropertyIsFollowedByASize()
    {
        return VerifyAsync("""
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    var parameter = new SqliteParameter();
                    parameter.Value = buffer;
                    parameter.Size = 8;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForValueAssignmentsOnOtherTypesAndMembers()
    {
        return VerifyAsync("""
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    var holder = new Holder();
                    holder.Value = buffer;
                    holder.Field = buffer;
                    var parameter = new SqliteParameter();
                    parameter.Value = new byte[8];
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherParameterReceivesTheSizeAfterTheValue()
    {
        return VerifyAsync("""
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    var first = new SqliteParameter();
                    var second = new SqliteParameter();
                    {|E128086:first.Value = buffer|};
                    second.Size = 8;
            """);
    }
}
