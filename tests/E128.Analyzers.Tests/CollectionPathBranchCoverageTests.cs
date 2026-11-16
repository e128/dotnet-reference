using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CollectionPathBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAQualifiedCollectionType()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionPathAnalyzer>("""
            class C
            {
                void M(System.Collections.Generic.List<string> {|E128053:paths|}) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForARecordPrimaryConstructorParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionPathAnalyzer>("""
            using System.Collections.Generic;

            record R(IReadOnlyList<string> {|E128053:paths|});
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAConstructorParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionPathAnalyzer>("""
            using System.Collections.Generic;

            class C
            {
                public C(IEnumerable<string> {|E128053:directories|}) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForTypesThatAreNotAStringCollection()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionPathAnalyzer>("""
            using System.Collections.Generic;

            class List<TA, TB>
            {
            }

            class C
            {
                void M(
                    string paths,
                    HashSet<string> setPaths,
                    List<int> intPaths,
                    List<string, string> pairPaths,
                    System.Collections.Generic.IList<int> qualifiedPaths,
                    System.Text.StringBuilder builderPaths,
                    List<string> names)
                {
                }

                delegate void Handler(string paths);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesTheTypeArgumentOfAQualifiedCollection()
    {
        return CodeFixTestRunner.RunAsync<CollectionPathAnalyzer, CollectionPathCodeFixProvider>(
            """
            class C
            {
                void M(System.Collections.Generic.List<string> {|E128053:filePaths|}) { }
            }
            """,
            """
            using System.IO;

            class C
            {
                void M(System.Collections.Generic.List<FileInfo> filePaths) { }
            }
            """);
    }
}
