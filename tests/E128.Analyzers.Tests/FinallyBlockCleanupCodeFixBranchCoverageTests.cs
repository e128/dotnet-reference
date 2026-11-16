using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FinallyBlockCleanupCodeFixBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_WrapsTheCleanupCallInATryCatchOfException()
    {
        return CodeFixTestRunner.RunAsync<FinallyBlockCleanupAnalyzer, FinallyBlockCleanupCodeFixProvider>(
            """
            using System.IO;
            class C
            {
                void M(string path)
                {
                    try { }
                    finally
                    {
                        {|E128057:Directory.Delete(path, true)|};
                    }
                }
            }
            """,
            """
            using System.IO;
            class C
            {
                void M(string path)
                {
                    try { }
                    finally
                    {
                        try
                        {
                            Directory.Delete(path, true);
                        }
                        catch (System.Exception)
                        {
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCleanupCallSitsInALocalDeclaration()
    {
        return CodeFixTestRunner.NoFixAsync<FinallyBlockCleanupAnalyzer, FinallyBlockCleanupCodeFixProvider>(
            """
            using System;
            using System.IO;
            class C
            {
                void M(string path)
                {
                    try { }
                    finally
                    {
                        Action cleanup = () => {|E128057:File.Delete(path)|};
                        cleanup();
                    }
                }
            }
            """);
    }
}
