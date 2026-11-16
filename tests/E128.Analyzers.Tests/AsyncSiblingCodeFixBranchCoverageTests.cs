using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncSiblingCodeFixBranchCoverageTests
{
    private const string DbPrefix = """
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

    [Fact]
    [Trait("Category", "CI")]
    public Task FileFix_Declines_ForAnUnqualifiedCall()
    {
        return CodeFixTestRunner.NoFixAsync<AsyncFileIoAnalyzer, AsyncFileIoCodeFixProvider>(
            """
            using static System.IO.File;

            class C
            {
                void M(string path)
                {
                    {|E128092:ReadAllText(path)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileFix_Declines_InALocalFunction()
    {
        return CodeFixTestRunner.NoFixAsync<AsyncFileIoAnalyzer, AsyncFileIoCodeFixProvider>(
            """
            using System.IO;

            class C
            {
                void M(string path)
                {
                    void Local() => {|E128092:File.ReadAllText(path)|};
                    Local();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileFix_Declines_InAConstructor()
    {
        return CodeFixTestRunner.NoFixAsync<AsyncFileIoAnalyzer, AsyncFileIoCodeFixProvider>(
            """
            using System.IO;

            class C
            {
                public C(string path)
                {
                    {|E128092:File.ReadAllText(path)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileFix_Declines_InALambdaInsideAMethod()
    {
        return CodeFixTestRunner.NoFixAsync<AsyncFileIoAnalyzer, AsyncFileIoCodeFixProvider>(
            """
            using System;
            using System.IO;

            class C
            {
                void M(string path)
                {
                    Action read = () => {|E128092:File.ReadAllText(path)|};
                    read();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileFix_Declines_InAnAnonymousMethodAndSimpleLambda()
    {
        return CodeFixTestRunner.NoFixAsync<AsyncFileIoAnalyzer, AsyncFileIoCodeFixProvider>(
            """
            using System;
            using System.IO;

            class C
            {
                void M(string path)
                {
                    Action anon = delegate { {|E128092:File.ReadAllText(path)|}; };
                    Action<int> simple = x => {|E128092:File.ReadAllText(path)|};
                    anon();
                    simple(1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DbFix_Declines_ForAConditionalAccessCall()
    {
        return CodeFixTestRunner.NoFixAsync<AsyncDbIoAnalyzer, AsyncDbIoCodeFixProvider>(
            DbPrefix + """

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
    public Task DbFix_Declines_InAConstructor()
    {
        return CodeFixTestRunner.NoFixAsync<AsyncDbIoAnalyzer, AsyncDbIoCodeFixProvider>(
            DbPrefix + """

            class C
            {
                public C(FakeConnection connection)
                {
                    {|E128093:connection.Open()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DbFix_Declines_InALocalFunction()
    {
        return CodeFixTestRunner.NoFixAsync<AsyncDbIoAnalyzer, AsyncDbIoCodeFixProvider>(
            DbPrefix + """

            class C
            {
                void M(FakeConnection connection)
                {
                    void Local() => {|E128093:connection.Open()|};
                    Local();
                }
            }
            """);
    }
}
