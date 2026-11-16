using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TempDirCleanupAnalyzerBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<TempDirCleanupAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAPropertyInitializer()
    {
        return VerifyAsync("""
            using System.IO;

            class {|E128054:MyTests|}
            {
                private string Root { get; } = Path.GetTempPath();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAConstructorThatStoresThePathInAProperty()
    {
        return VerifyAsync("""
            using System.IO;

            class {|E128054:MyTests|}
            {
                private string Root { get; set; } = "";

                public MyTests()
                {
                    Root = Path.GetTempPath();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAConstructorThatKeepsThePathInALocal()
    {
        return VerifyAsync("""
            using System.IO;

            class {|E128054:MyTests|}
            {
                public MyTests()
                {
                    var local = Path.GetTempPath();
                    _ = local;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForANestedClassOnlyOnTheInnerClass()
    {
        return VerifyAsync("""
            using System.IO;

            class Outer
            {
                class {|E128054:Inner|}
                {
                    private readonly string _root = Path.GetTempPath();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForTheSameCallInAPropertyAccessorOrLocalFunction()
    {
        return VerifyAsync("""
            using System.IO;

            class MyTests
            {
                public string Root
                {
                    get { return Path.GetTempPath(); }
                }

                public MyTests()
                {
                    string Local() { return Path.GetTempPath(); }
                    _ = Local();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForTypesThatAreNotClassesAndForOtherPathTypes()
    {
        return VerifyAsync("""
            using System.IO;

            struct StructTests
            {
                private static readonly string s_root = Path.GetTempPath();
            }

            record RecordTests
            {
                public string Root { get; } = Path.GetTempPath();
            }

            static class Path2
            {
                public static string GetTempPath() { return ""; }
            }

            namespace Other
            {
                static class Path
                {
                    public static string GetTempPath() { return ""; }
                }

                class Fake
                {
                    private readonly string _root = Path.GetTempPath();
                    private readonly string _again = Path2.GetTempPath();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCleanupInterfaceIsFromAnotherNamespace()
    {
        return VerifyAsync("""
            using System.IO;

            namespace Other
            {
                interface IDisposable
                {
                }
            }

            class {|E128054:MyTests|} : Other.IDisposable
            {
                private readonly string _root = Path.GetTempPath();
            }
            """);
    }
}
