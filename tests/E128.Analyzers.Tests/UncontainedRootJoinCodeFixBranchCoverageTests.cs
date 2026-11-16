using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UncontainedRootJoinCodeFixBranchCoverageTests
{
    private const string Containment = """
        namespace Pug.Core
        {
            public static class FilePathContainment
            {
                public static System.IO.FileInfo ResolveInside(System.IO.DirectoryInfo root, string leaf)
                    => new System.IO.FileInfo(leaf);
            }
        }
        """;

    private static Task NoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<UncontainedRootJoinAnalyzer, UncontainedRootJoinCodeFixProvider>(
            source + "\n" + Containment);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_HandlesAQualifiedTypeAndParenthesizedOperands()
    {
        return CodeFixTestRunner.RunAsync<UncontainedRootJoinAnalyzer, UncontainedRootJoinCodeFixProvider>(
            """
            using Pug.Core;
            using System.IO;

            class C
            {
                FileInfo M(DirectoryInfo root, string leaf)
                {
                    return new System.IO.FileInfo(({|E128222:Path.Combine((root.FullName), (leaf))|}));
                }
            }
            """ + "\n" + Containment,
            """
            using Pug.Core;
            using System.IO;

            class C
            {
                FileInfo M(DirectoryInfo root, string leaf)
                {
                    return FilePathContainment.ResolveInside(root, (leaf));
                }
            }
            """ + "\n" + Containment);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAnotherTypeIsNamedFileInfo()
    {
        return NoFixAsync("""
            using System.IO;

            namespace Other
            {
                public sealed class FileInfo
                {
                    public FileInfo(string path) { }
                }
            }

            class C
            {
                Other.FileInfo M(DirectoryInfo root, string leaf)
                {
                    return new Other.FileInfo({|E128222:Path.Combine(root.FullName, leaf)|});
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheJoinFeedsADifferentConstructionOrATargetTypedOne()
    {
        return NoFixAsync("""
            using System.IO;

            class C
            {
                void M(DirectoryInfo root, string leaf)
                {
                    var reader = new StreamReader({|E128222:Path.Combine(root.FullName, leaf)|});
                    FileInfo info = new({|E128222:Path.Combine(root.FullName, leaf)|});
                    var text = {|E128222:Path.Combine(root.FullName, leaf)|};
                }
            }
            """);
    }
}
