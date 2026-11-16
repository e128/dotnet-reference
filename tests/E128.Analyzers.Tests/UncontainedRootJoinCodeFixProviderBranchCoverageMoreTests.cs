using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UncontainedRootJoinCodeFixProviderBranchCoverageMoreTests
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
    public Task NoFix_WhenAnAliasQualifiedFileInfoTypeIsNotSystemFileInfo()
    {
        return NoFixAsync("""
            using System.IO;

            class FileInfo
            {
                public FileInfo(string path) { }
            }

            class C
            {
                object M(DirectoryInfo root, string leaf)
                {
                    return new global::FileInfo({|E128222:Path.Combine(root.FullName, leaf)|});
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheJoinFeedsAGenericTypeConstruction()
    {
        return NoFixAsync("""
            using System.IO;

            class Wrapper<T>
            {
                public Wrapper(string path) { }
            }

            class C
            {
                object M(DirectoryInfo root, string leaf)
                {
                    return new Wrapper<string>({|E128222:Path.Combine(root.FullName, leaf)|});
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheJoinFeedsAQualifiedTypeThatIsNotNamedFileInfo()
    {
        return NoFixAsync("""
            using System.IO;

            namespace Other
            {
                public sealed class Reader
                {
                    public Reader(string path) { }
                }
            }

            class C
            {
                object M(DirectoryInfo root, string leaf)
                {
                    return new Other.Reader({|E128222:Path.Combine(root.FullName, leaf)|});
                }
            }
            """);
    }
}
