using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UncontainedRootJoinAnalyzerTests
{
    private const string ContainmentStub = """

        namespace Pug.Core
        {
            public static class FilePathContainment
            {
                public static bool IsInside(System.IO.FileInfo file, System.IO.DirectoryInfo root) => true;

                public static bool ValidateDomainPath(string path, System.IO.DirectoryInfo root) => true;

                public static System.IO.FileInfo ResolveInside(System.IO.DirectoryInfo root, string leaf) => new(leaf);
            }
        }
        """;

    [Fact]
    [Trait("Category", "CI")]
    public Task CallerSuppliedLeaf_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>("""
            using System.IO;

            class C
            {
                string M(DirectoryInfo root, string callerName)
                {
                    return {|E128222:Path.Combine(root.FullName, callerName)|};
                }
            }
            """ + ContainmentStub);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralLeaf_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>("""
            using System.IO;

            class C
            {
                string M(DirectoryInfo root)
                {
                    return Path.Combine(root.FullName, "safe.txt");
                }
            }
            """ + ContainmentStub);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetFileNameLeaf_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>("""
            using System.IO;

            class C
            {
                string M(DirectoryInfo root, string callerName)
                {
                    return Path.Combine(root.FullName, Path.GetFileName(callerName));
                }
            }
            """ + ContainmentStub);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetRandomFileNameLeaf_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>("""
            using System.IO;

            class C
            {
                string M(DirectoryInfo root)
                {
                    return Path.Combine(root.FullName, Path.GetRandomFileName());
                }
            }
            """ + ContainmentStub);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NumericInterpolationLeaf_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>("""
            using System.IO;

            class C
            {
                string M(DirectoryInfo root, int index)
                {
                    return Path.Combine(root.FullName, $"entry-{index}.txt");
                }
            }
            """ + ContainmentStub);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NumericInterpolationWithParentReference_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>("""
            using System.IO;

            class C
            {
                string M(DirectoryInfo root, int index)
                {
                    return {|E128222:Path.Combine(root.FullName, $"../{index}.txt")|};
                }
            }
            """ + ContainmentStub);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringInterpolationLeaf_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>("""
            using System.IO;

            class C
            {
                string M(DirectoryInfo root, string callerName)
                {
                    return {|E128222:Path.Combine(root.FullName, $"entry-{callerName}.txt")|};
                }
            }
            """ + ContainmentStub);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task JoinPassedToIsInside_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>("""
            using System.IO;
            using Pug.Core;

            class C
            {
                string M(DirectoryInfo root, string callerName)
                {
                    var path = Path.Combine(root.FullName, callerName);
                    if (!FilePathContainment.IsInside(new FileInfo(path), root))
                    {
                        throw new System.ArgumentException("outside the root");
                    }

                    return path;
                }
            }
            """ + ContainmentStub);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnrelatedIsInside_StillFires()
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>("""
            using System.IO;
            using Pug.Core;

            class C
            {
                void M(DirectoryInfo root, string callerName)
                {
                    var other = new FileInfo(Path.Combine(root.FullName, "safe.txt"));
                    if (!FilePathContainment.IsInside(other, root))
                    {
                        throw new System.ArgumentException("outside the root");
                    }

                    var escaped = {|E128222:Path.Combine(root.FullName, callerName)|};
                    _ = escaped;
                }
            }
            """ + ContainmentStub);
    }
}
