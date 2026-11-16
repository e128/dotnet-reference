using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RecursiveDeleteRootBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<RecursiveDeleteRootAnalyzer>(code);
    }

    private static Task VerifyNameAsync(string code, string name)
    {
        var expected = new DiagnosticResult("E128184", DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments(name);

        return AnalyzerTestRunner.VerifyAsync<RecursiveDeleteRootAnalyzer>(code, expected);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsNotARecursiveDirectoryDelete()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class Own
            {
                public void Delete(string path, bool recursive) { }
            }

            class C
            {
                void Delete(string path, bool recursive) { }

                void M(string path, bool flag, Own own, dynamic dyn)
                {
                    Directory.Delete(path);
                    Directory.Delete(path, false);
                    Directory.Delete(path, flag);
                    File.Delete(path);
                    own.Delete(path, true);
                    Delete(path, true);
                    dyn.Delete(path, true);
                }

                void ThroughDelegate(string path, Action<string, bool> Delete)
                {
                    Delete(path, true);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NamedArgumentsInAnyOrder()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M(string path, DirectoryInfo directory)
                {
                    {|E128184:Directory.Delete(path: path, recursive: true)|};
                    {|E128184:Directory.Delete(recursive: true, path: path)|};
                    {|E128184:Directory.Delete(path, recursive: true)|};
                    {|E128184:directory.Delete(recursive: true)|};
                    directory.Delete();
                    Directory.Delete(path: path, recursive: false);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ADeleteThroughAStaticUsing()
    {
        return VerifyAsync("""
            using static System.IO.Directory;
            class C
            {
                void M(string path)
                {
                    {|E128184:Delete(path, true)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RootedAndDriveRootConstants()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                const string Rooted = "/var/data";
                const string Relative = "data";

                void M()
                {
                    {|E128184:Directory.Delete("/", true)|};
                    {|E128184:Directory.Delete(" / ", true)|};
                    {|E128184:Directory.Delete("\\", true)|};
                    {|E128184:Directory.Delete("C:", true)|};
                    {|E128184:Directory.Delete("c:/", true)|};
                    {|E128184:Directory.Delete("D:\\", true)|};
                    {|E128184:Directory.Delete("C:data", true)|};
                    {|E128184:Directory.Delete("\\\\server\\share", true)|};
                    {|E128184:Directory.Delete(Rooted, true)|};
                    {|E128184:Directory.Delete("/" + "var", true)|};
                    Directory.Delete(Relative, true);
                    Directory.Delete("data", true);
                    Directory.Delete("a", true);
                    Directory.Delete("1:", true);
                    Directory.Delete(("data"), true);
                    Directory.Delete((string)"data", true);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Classifies_PathsThroughGetFullPathAndCombine()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M(string path)
                {
                    Directory.Delete(Path.GetFullPath("data"), true);
                    Directory.Delete(Path.GetFullPath(Path.GetTempPath()), true);
                    Directory.Delete(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "x")), true);
                    Directory.Delete(Path.Combine(Path.Combine(Path.GetTempPath(), "a"), "b"), true);
                    Directory.Delete(Path.Combine("data", path), true);
                    {|E128184:Directory.Delete(Path.GetFullPath("/etc"), true)|};
                    {|E128184:Directory.Delete(Path.GetFullPath(path), true)|};
                    {|E128184:Directory.Delete(Path.Combine(path), true)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePathIsComputedByACallTheRuleCannotTrace()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                static string Compute() => "x";

                void M(string path)
                {
                    Directory.Delete(Compute(), true);
                    Directory.Delete(Path.Combine(Compute(), "x"), true);
                    Directory.Delete(new string('a', 3), true);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Follows_LocalAliasesAndCastsToTheirSource()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M(string path)
                {
                    var a = path;
                    var b = (string)a;
                    {|E128184:Directory.Delete(b, true)|};

                    var temp = Path.GetTempPath();
                    var child = temp;
                    Directory.Delete(child, true);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Classifies_DirectoryInfoCreationsAndLocals()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M(string path)
                {
                    {|E128184:new DirectoryInfo(path).Delete(true)|};
                    new DirectoryInfo(Path.GetTempPath()).Delete(true);
                    new DirectoryInfo("data").Delete(true);
                    {|E128184:new DirectoryInfo("/data").Delete(true)|};

                    var info = new DirectoryInfo(path);
                    {|E128184:info.Delete(true)|};

                    var safe = new DirectoryInfo(Path.GetTempPath());
                    safe.Delete(true);
                    safe.Delete(false);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Classifies_PrivateHelperParametersByTheirCallSites()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                static string Compute() => "x";

                void SafeOnly(string target)
                {
                    Directory.Delete(target, true);
                }

                void UnsafeAndSafe(string target)
                {
                    {|E128184:Directory.Delete(target, true)|};
                }

                void WithUnknown(string target)
                {
                    Directory.Delete(target, true);
                }

                void UnknownThenUnsafe(string target)
                {
                    {|E128184:Directory.Delete(target, true)|};
                }

                void Run(string path)
                {
                    SafeOnly(Path.GetTempPath());
                    SafeOnly(target: "data");
                    UnsafeAndSafe(Path.GetTempPath());
                    UnsafeAndSafe(path);
                    WithUnknown(Compute());
                    WithUnknown(Path.GetTempPath());
                    UnknownThenUnsafe(Compute());
                    UnknownThenUnsafe(path);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Classifies_PrivateHelperWithoutCallersWithSelfCallsAndWithOverloads()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void NoCaller(string target)
                {
                    {|E128184:Directory.Delete(target, true)|};
                }

                void Recursive(string target, bool again)
                {
                    {|E128184:Directory.Delete(target, true)|};
                    if (again)
                    {
                        Recursive(target, false);
                    }
                }

                void Overloaded(string target)
                {
                    Directory.Delete(target, true);
                }

                void Overloaded(string target, int extra)
                {
                }

                void Run(string path)
                {
                    Recursive(Path.GetTempPath(), true);
                    Overloaded(Path.GetTempPath());
                    Overloaded(path, 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ParametersThatNoPrivateMethodOwns()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                public C(string path)
                {
                    {|E128184:Directory.Delete(path, true)|};
                }

                internal void Internal(string path)
                {
                    {|E128184:Directory.Delete(path, true)|};
                }

                string this[string key]
                {
                    get
                    {
                        {|E128184:Directory.Delete(key, true)|};
                        return key;
                    }
                }

                void M()
                {
                    void Local(string path)
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    Action<string> lambda = path => {|E128184:Directory.Delete(path, true)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_APrivateHelperOfATypeSplitAcrossFiles()
    {
        return AnalyzerTestRunner.VerifyAsync<RecursiveDeleteRootAnalyzer>(
            [
                """
                using System.IO;
                partial class C
                {
                    private void Clean(string target)
                    {
                        {|E128184:Directory.Delete(target, true)|};
                    }
                }
                """,
                """
                using System.IO;
                partial class C
                {
                    void Run()
                    {
                        Clean(Path.GetTempPath());
                    }
                }
                """
            ]);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Classifies_PrivateHelperCalledFromAPartOfTheSameFile()
    {
        return VerifyAsync("""
            using System.IO;
            partial class C
            {
                private void Clean(string target)
                {
                    Directory.Delete(target, true);
                }
            }

            partial class C
            {
                void Run()
                {
                    Clean(Path.GetTempPath());
                }
            }

            record struct R(int Value)
            {
                private void Clean(string target)
                {
                    Directory.Delete(target, true);
                }

                void Run()
                {
                    Clean(Path.GetTempPath());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Reports_TheDisplayNameOfEachPathShape()
    {
        await VerifyNameAsync(
                """
                using System.IO;
                class C
                {
                    void M(string path)
                    {
                        {|#0:Directory.Delete(path, true)|};
                    }
                }
                """,
                "path");
        await VerifyNameAsync(
                """
                using System.IO;
                class C
                {
                    string root = "x";

                    void M()
                    {
                        {|#0:Directory.Delete(root, true)|};
                    }
                }
                """,
                "root");
        await VerifyNameAsync(
                """
                using System.IO;
                class C
                {
                    string Root { get; set; } = "x";

                    void M()
                    {
                        {|#0:Directory.Delete((string)Root, true)|};
                    }
                }
                """,
                "Root");
        await VerifyNameAsync(
                """
                using System.IO;
                class C
                {
                    void M(string path)
                    {
                        {|#0:Directory.Delete(Path.Combine(path, "cache"), true)|};
                    }
                }
                """,
                "Path.Combine(path, \"cache\")");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnEnclosingGuardProvesTheStartsWithResult()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                void M(string path, bool flag, string[] args)
                {
                    var root = Path.GetTempPath();
                    if (path.StartsWith(root, StringComparison.Ordinal) && flag)
                    {
                        Directory.Delete(path, true);
                    }

                    if ((path.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
                    {
                        Directory.Delete(path, true);
                    }

                    if (path.StartsWith(value: root, comparisonType: StringComparison.Ordinal))
                    {
                        Directory.Delete(path, true);
                    }

                    if (args[0].StartsWith(root, StringComparison.Ordinal))
                    {
                        Directory.Delete(args[ 0 ], true);
                    }

                    if (Path.GetFullPath(path).StartsWith(root, StringComparison.Ordinal))
                    {
                        Directory.Delete(Path.GetFullPath(path), true);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheEnclosingGuardProvesTooLittle()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class Probe
            {
                public bool StartsWith(string value, StringComparison comparison) => true;
            }

            class C
            {
                bool Check(string value) => true;

                void M(string path, string other, bool flag, Probe probe, StringComparison comparison, string[] args)
                {
                    var root = Path.GetTempPath();

                    if (path.StartsWith(root, StringComparison.Ordinal) || flag)
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith(root, StringComparison.Ordinal))
                    {
                    }
                    else
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (other.StartsWith(root, StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (probe.StartsWith(root, comparison))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (Check(path))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheEnclosingGuardComparesLoosely()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                void M(string path, string other, StringComparison comparison, string[] args)
                {
                    var root = Path.GetTempPath();

                    if (path.StartsWith(root, comparison))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith('/'))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (args[0].StartsWith(root, StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith(root, StringComparison.Ordinal))
                    {
                        path = other;
                        {|E128184:Directory.Delete(path, true)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePrecedingStatementExitsUnlessTheStartsWithResultIsTrue()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                void M(string path, bool flag, int count)
                {
                    var root = Path.GetTempPath();

                    if (!(path.StartsWith(root, StringComparison.Ordinal)) || flag)
                    {
                        throw new InvalidOperationException();
                    }

                    Directory.Delete(path, true);

                    if (path.StartsWith(root, StringComparison.Ordinal))
                    {
                        count++;
                        --count;
                        ++count;
                        count--;
                        Directory.Delete(path, true);
                    }

                    if (!path.StartsWith(root, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (flag)
                    {
                        Directory.Delete(path, true);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePrecedingGuardProvesTooLittle()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                void M(string path, string other, bool flag)
                {
                    var root = Path.GetTempPath();

                    if (path.StartsWith(root, StringComparison.Ordinal))
                    {
                        return;
                    }

                    {|E128184:Directory.Delete(path, true)|};

                    if (!path.StartsWith(root, StringComparison.Ordinal) && flag)
                    {
                        return;
                    }

                    {|E128184:Directory.Delete(path, true)|};

                    if (!!path.StartsWith(root, StringComparison.Ordinal))
                    {
                        return;
                    }

                    {|E128184:Directory.Delete(path, true)|};

                    if (!path.StartsWith(root, StringComparison.Ordinal))
                    {
                        return;
                    }
                    else
                    {
                        flag = true;
                    }

                    {|E128184:Directory.Delete(path, true)|};

                    if (!path.StartsWith(root, StringComparison.Ordinal))
                    {
                        return;
                    }

                    flag = !flag;
                    {|E128184:Directory.Delete(path, true)|};

                    if (!path.StartsWith(root, StringComparison.Ordinal))
                    {
                        return;
                    }

                    path = other;
                    {|E128184:Directory.Delete(path, true)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenNoStatementDirectlyPrecedesTheDeleteWithAGuard()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                string Holder
                {
                    get
                    {
                        {|E128184:Directory.Delete(Holder, true)|};
                        return string.Empty;
                    }
                }

                void M(string path, bool flag)
                {
                    {|E128184:Directory.Delete(path, true)|};
                }

                void FarFromTheGuard(string path, bool flag)
                {
                    var root = Path.GetTempPath();
                    if (!path.StartsWith(root, StringComparison.Ordinal))
                    {
                        return;
                    }

                    var x = 1;
                    {|E128184:Directory.Delete(path, true)|};
                }

                void Nested(string path, bool flag)
                {
                    var root = Path.GetTempPath();
                    if (!path.StartsWith(root, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (flag)
                    {
                        Directory.Delete(path, true);
                    }

                    Action inner = () => {|E128184:Directory.Delete(path, true)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardRootIsBoundedByASeparator()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                const string Fixed = "data/";
                const string Bare = "data";

                void M(string path)
                {
                    if (path.StartsWith(Fixed, StringComparison.Ordinal))
                    {
                        Directory.Delete(path, true);
                    }

                    if (path.StartsWith(Bare + "/", StringComparison.Ordinal))
                    {
                        Directory.Delete(path, true);
                    }

                    if (path.StartsWith(Path.GetTempPath() + "/", StringComparison.Ordinal))
                    {
                        Directory.Delete(path, true);
                    }

                    if (path.StartsWith(Path.GetTempPath() + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
                    {
                        Directory.Delete(path, true);
                    }

                    if (path.StartsWith(Path.GetTempPath() + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    {
                        Directory.Delete(path, true);
                    }

                    if (path.StartsWith((Fixed), StringComparison.Ordinal))
                    {
                        Directory.Delete(path, true);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardRootIsNotBoundedOrNotSafe()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class Other
            {
                public const char DirectorySeparatorChar = '/';
            }

            class C
            {
                const string Bare = "data";
                void M(string path, char sep)
                {
                    if (path.StartsWith(Bare, StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith(Path.GetTempPath() + sep, StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith(Path.GetTempPath() + Path.PathSeparator, StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith(Path.GetTempPath() + Other.DirectorySeparatorChar, StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith(Path.GetTempPath() + "x", StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardRootIsComputedAtRunTime()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                static readonly string Dynamic = Environment.GetEnvironmentVariable("ROOT") ?? "x/";

                static string Compute() => "x/";

                void M(string path, string param)
                {
                    string uninitialized;
                    uninitialized = "x/";

                    if (path.StartsWith(param + "/", StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith(Dynamic, StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith(Compute(), StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }

                    if (path.StartsWith(uninitialized, StringComparison.Ordinal))
                    {
                        {|E128184:Directory.Delete(path, true)|};
                    }
                }
            }
            """);
    }
}
