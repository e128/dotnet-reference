using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RecursiveDeleteRootAnalyzerBranchCoverageGapTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<RecursiveDeleteRootAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheDeleteIsReachedThroughAConditionalAccessOrAnInvokedResult()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                static Action<string, bool> Resolve() => Directory.Delete;

                void M(string path)
                {
                    var info = new DirectoryInfo(Path.GetTempPath());
                    info?.Delete(true);
                    Resolve()(path, true);
                    (Resolve())(path, true);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalHasNoInitializerOrComesFromAnIteration()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                string[] Names() => new[] { "a" };

                void M(string input)
                {
                    string assigned;
                    assigned = input;
                    Directory.Delete(assigned, true);

                    foreach (var name in Names())
                    {
                        Directory.Delete(name, true);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCombineHasNoArguments()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M()
                {
                    Directory.Delete(Path.Combine(), true);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLiteralIsEmptyOrHasNoDriveColon()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M()
                {
                    Directory.Delete("", true);
                    Directory.Delete("ab", true);
                    Directory.Delete("x", true);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardRootIsAnEmptyString()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                void M(string path)
                {
                    if (!path.StartsWith("", StringComparison.Ordinal))
                    {
                        return;
                    }

                    {|E128184:Directory.Delete(path, true)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardRootIsARootPlusAnEmptySuffix()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                void M(string path)
                {
                    if (path.StartsWith("/srv/app" + "", StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenUnrelatedUnaryOperatorsRunBetweenTheGuardAndTheDelete()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                void M(string path, int count)
                {
                    if (path.StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
                    {
                        --count;
                        count--;
                        ++count;
                        count++;
                        var negated = -count;
                        var forgiven = path!;
                        GC.KeepAlive(forgiven);
                        GC.KeepAlive(negated);
                        Directory.Delete(path, true);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFrameworkReferencesHaveNoDirectoryType()
    {
        return AnalyzerTestRunner.VerifyAsync<RecursiveDeleteRootAnalyzer>(
            """
            class Directory
            {
                public static void Delete(string path, bool recursive) { }
            }

            class C
            {
                void M(string path)
                {
                    Directory.Delete(path, true);
                }
            }
            """,
            options: new AnalyzerTestOptions { References = ReferenceAssemblies.NetStandard.NetStandard10 });
    }
}
