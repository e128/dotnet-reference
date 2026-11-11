using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UncontainedRootJoinCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoJoin_ReplacedWithResolveInside()
    {
        const string source = """
                              using System.IO;

                              class C
                              {
                                  FileInfo M(DirectoryInfo root, string leaf)
                                  {
                                      return new FileInfo({|E128222:Path.Combine(root.FullName, leaf)|});
                                  }
                              }

                              namespace Pug.Core
                              {
                                  public static class FilePathContainment
                                  {
                                      public static FileInfo ResolveInside(DirectoryInfo root, string leaf)
                                          => new FileInfo(leaf);
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using Pug.Core;
                                 using System.IO;

                                 class C
                                 {
                                     FileInfo M(DirectoryInfo root, string leaf)
                                     {
                                         return FilePathContainment.ResolveInside(root, leaf);
                                     }
                                 }

                                 namespace Pug.Core
                                 {
                                     public static class FilePathContainment
                                     {
                                         public static FileInfo ResolveInside(DirectoryInfo root, string leaf)
                                             => new FileInfo(leaf);
                                     }
                                 }
                                 """;

        return CodeFixTestRunner.RunAsync<UncontainedRootJoinAnalyzer,
            UncontainedRootJoinCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task JoinWithoutFileInfo_NoFixOffered()
    {
        return CodeFixTestRunner.NoFixAsync<UncontainedRootJoinAnalyzer,
            UncontainedRootJoinCodeFixProvider>("""
            using System.IO;
            using Pug.Core;

            class C
            {
                string M(DirectoryInfo root, string callerName)
                {
                    return {|E128222:Path.Combine(root.FullName, callerName)|};
                }
            }

            namespace Pug.Core
            {
                public static class FilePathContainment
                {
                    public static FileInfo ResolveInside(DirectoryInfo root, string leaf) => new FileInfo(leaf);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MissingContainmentType_NoFixOffered()
    {
        return CodeFixTestRunner.NoFixAsync<UncontainedRootJoinAnalyzer,
            UncontainedRootJoinCodeFixProvider>("""
            using System.IO;

            class C
            {
                FileInfo M(DirectoryInfo root, string callerName)
                {
                    return new FileInfo({|E128222:Path.Combine(root.FullName, callerName)|});
                }
            }
            """);
    }
}
