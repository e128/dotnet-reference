using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TempDirCleanupAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<TempDirCleanupAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheConstructorPassesThePathStraightToACall()
    {
        return VerifyAsync("""
            using System.IO;

            class {|E128054:MyTests|}
            {
                public MyTests()
                {
                    Directory.CreateDirectory(Path.GetTempPath());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsInAnExpressionBodiedMemberOrOperator()
    {
        return VerifyAsync("""
            using System;
            using System.IO;

            class MyTests
            {
                public string Root => Path.GetTempPath();

                public Func<string> Factory => () =>
                {
                    var path = Path.GetTempPath();
                    return path;
                };

                public static MyTests operator +(MyTests left, MyTests right)
                {
                    var path = Path.GetTempPath();
                    return left;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsInAParameterDefault()
    {
        return AnalyzerTestRunner.VerifyAsync<TempDirCleanupAnalyzer>(
            """
            using System.IO;

            class MyTests
            {
                public void Run(string root = Path.GetTempPath())
                {
                }
            }
            """,
            expected: null,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheClassImplementsIAsyncDisposable()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            using System.Threading.Tasks;

            class MyTests : IAsyncDisposable
            {
                private readonly string _root = Path.GetTempPath();

                public ValueTask DisposeAsync() => default;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheClassImplementsAnAsyncLifetimeStandIn()
    {
        return VerifyAsync("""
            using System.IO;

            interface IAsyncLifetime
            {
            }

            class MyTests : IAsyncLifetime
            {
                private readonly string _root = Path.GetTempPath();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheClassImplementsOnlyAnUnrelatedInterface()
    {
        return VerifyAsync("""
            using System;
            using System.IO;

            class {|E128054:MyTests|} : IComparable
            {
                private readonly string _root = Path.GetTempPath();

                public int CompareTo(object obj) => 0;
            }
            """);
    }
}
