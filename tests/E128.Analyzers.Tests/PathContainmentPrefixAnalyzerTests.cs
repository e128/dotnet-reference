using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PathContainmentPrefixAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFullNameIsTestedAgainstABareRoot()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file, string root)
                               {
                                   if ([|file.FullName.StartsWith(root)|]) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheResolvedLocalIsTestedAgainstABareRoot()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file, string root)
                               {
                                   var path = Path.GetFullPath(file.FullName);
                                   if ([|path.StartsWith(root)|]) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheComparisonUsesStringComparison()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System;
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file, string root)
                               {
                                   if ([|file.FullName.StartsWith(root, StringComparison.Ordinal)|]) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheRootIsReassignedWithoutASeparator()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file, string root)
                               {
                                   var safeRoot = root + Path.DirectorySeparatorChar;
                                   safeRoot = root;
                                   if ([|file.FullName.StartsWith(safeRoot)|]) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRootAppendsASeparator()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System;
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file, string root)
                               {
                                   if (file.FullName.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRootAppendsASeparatorLiteral()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file, string root)
                               {
                                   if (file.FullName.StartsWith(root + "/")) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRootIsTrimmedThenSeparated()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file, string root)
                               {
                                   if (file.FullName.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar)) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRootLocalAppendsASeparator()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file, string root)
                               {
                                   string safeRoot;
                                   safeRoot = root + Path.DirectorySeparatorChar;
                                   if (file.FullName.StartsWith(safeRoot)) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRootIsAnInterpolatedStringWithASeparator()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file, string root)
                               {
                                   if (file.FullName.StartsWith($"{root}/")) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFieldIsReassignedToASafeValue()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               private string _path = Path.GetFullPath("x");

                               void Run(string root)
                               {
                                   _path = "/safe";
                                   if (_path.StartsWith(root)) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsNotAResolvedPath()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           class C
                           {
                               void Run(string name, string prefix)
                               {
                                   if (name.StartsWith(prefix)) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheOverloadTakesAChar()
    {
        return AnalyzerTestRunner.VerifyAsync<PathContainmentPrefixAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file)
                               {
                                   if (file.FullName.StartsWith('/')) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_WrapsTheRootWithASeparator()
    {
        return CodeFixTestRunner.RunAsync<PathContainmentPrefixAnalyzer, PathContainmentPrefixCodeFixProvider>(
            """
            using System.IO;

            class C
            {
                void Run(FileInfo file, string root)
                {
                    if ([|file.FullName.StartsWith(root)|]) { }
                }
            }
            """,
            """
            using System.IO;

            class C
            {
                void Run(FileInfo file, string root)
                {
                    if (file.FullName.StartsWith(global::System.IO.Path.TrimEndingDirectorySeparator(root) + global::System.IO.Path.DirectorySeparatorChar)) { }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesTheQualifiedPathWithoutAUsing()
    {
        return CodeFixTestRunner.RunAsync<PathContainmentPrefixAnalyzer, PathContainmentPrefixCodeFixProvider>(
            """
            class C
            {
                void Run(System.IO.FileInfo file, string root)
                {
                    if ([|file.FullName.StartsWith(root)|]) { }
                }
            }
            """,
            """
            class C
            {
                void Run(System.IO.FileInfo file, string root)
                {
                    if (file.FullName.StartsWith(global::System.IO.Path.TrimEndingDirectorySeparator(root) + global::System.IO.Path.DirectorySeparatorChar)) { }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheRootIsANullableString()
    {
        return CodeFixTestRunner.NoFixAsync<PathContainmentPrefixAnalyzer, PathContainmentPrefixCodeFixProvider>(
            """
            #nullable enable

            using System.IO;

            class C
            {
                void Run(FileInfo file, string? root)
                {
                    if ([|file.FullName.StartsWith(root)|]) { }
                }
            }
            """);
    }
}
