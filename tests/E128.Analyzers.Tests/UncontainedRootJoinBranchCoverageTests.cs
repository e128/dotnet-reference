using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UncontainedRootJoinBranchCoverageTests
{
    private const string Guards = """
        static class Guard
        {
            public static bool IsInside(object path) => true;

            public static void ValidateDomainPath(object path) { }

            public static void ResolveInside(object path) { }

            public static bool IsInside<T>(T path) => true;
        }
        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>(code + "\n" + Guards);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsNotAnUncontainedPathCombine()
    {
        return VerifyAsync("""
            using System.IO;
            class Own
            {
                public static string Combine(string a, string b) => a + b;
            }

            class C
            {
                string M(DirectoryInfo root, string leaf, string third)
                {
                    var a = Path.Join(root.FullName, leaf);
                    var b = Path.Combine(root.FullName, leaf, third);
                    var c = Own.Combine(root.FullName, leaf);
                    var d = Path.Combine(root.Name, leaf);
                    var e = Path.Combine(leaf, root.FullName);
                    var f = Path.Combine(leaf, leaf);
                    var g = Combine(leaf, leaf);
                    return a + b + c + d + e + f + g;
                }

                string Combine(string a, string b) => a + b;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ParenthesizedRootAndLeafThatIsNotANameProducer()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                string Name(string value) => value;

                string M(DirectoryInfo root, string leaf)
                {
                    var a = {|E128222:Path.Combine((root.FullName), leaf)|};
                    var b = {|E128222:Path.Combine(root.FullName, Name(leaf))|};
                    var c = {|E128222:Path.Combine(root.FullName, leaf.Trim())|};
                    var d = {|E128222:Path.Combine(root.FullName, (leaf))|};
                    return a + b + c + d;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLeafComesFromANameProducer()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                static string SanitizeFileName(string value) => value;

                string M(DirectoryInfo root, string leaf)
                {
                    var a = Path.Combine(root.FullName, Path.GetFileNameWithoutExtension(leaf));
                    var b = Path.Combine(root.FullName, Path.ChangeExtension(leaf, ".txt"));
                    var c = Path.Combine(root.FullName, Path.GetTempFileName());
                    var d = Path.Combine(root.FullName, (Path.GetFileName(leaf)));
                    var e = Path.Combine(root.FullName, SanitizeFileName(leaf));
                    return a + b + c + d + e;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Handles_InterpolatedLeavesByTheirHolesAndText()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                string M(DirectoryInfo root, int index, long big, double ratio, string text, object boxed, decimal money)
                {
                    var a = Path.Combine(root.FullName, $"file{index}.txt");
                    var b = Path.Combine(root.FullName, $"{big}-{ratio}-{money}");
                    var c = {|E128222:Path.Combine(root.FullName, $"file{text}.txt")|};
                    var d = {|E128222:Path.Combine(root.FullName, $"file{boxed}")|};
                    var e = {|E128222:Path.Combine(root.FullName, $"a\\{index}")|};
                    var f = {|E128222:Path.Combine(root.FullName, $"..{index}")|};
                    var g = {|E128222:Path.Combine(root.FullName, $"{index}/x")|};
                    var h = Path.Combine(root.FullName, $"plain");
                    var i = {|E128222:Path.Combine(root.FullName, $"{index}" + text)|};
                    return a + b + c + d + e + f + g + h + i;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAGuardCallReceivesTheJoinThroughLocalsFieldsOrAWrapper()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                private string stored = "";

                private string Prop { get; set; } = "";

                void ByLocal(DirectoryInfo root, string leaf)
                {
                    var joined = Path.Combine(root.FullName, leaf);
                    Guard.IsInside(joined);
                }

                void ByChain(DirectoryInfo root, string leaf)
                {
                    var first = Path.Combine(root.FullName, leaf);
                    var second = first;
                    var third = (second);
                    Guard.ValidateDomainPath(third);
                }

                void ByAssignment(DirectoryInfo root, string leaf)
                {
                    string target;
                    target = Path.Combine(root.FullName, leaf);
                    Guard.ResolveInside(target);
                }

                void ByField(DirectoryInfo root, string leaf)
                {
                    stored = Path.Combine(root.FullName, leaf);
                    Guard.IsInside(this.stored);
                }

                void ByThisProperty(DirectoryInfo root, string leaf)
                {
                    this.Prop = Path.Combine(root.FullName, leaf);
                    Guard.IsInside(this.Prop);
                }

                void ByWrapper(DirectoryInfo root, string leaf)
                {
                    Guard.IsInside(new FileInfo(Path.Combine(root.FullName, leaf)));
                    Guard.IsInside((Path.Combine(root.FullName, leaf)));
                }

                void ByGeneric(DirectoryInfo root, string leaf)
                {
                    var joined = Path.Combine(root.FullName, leaf);
                    Guard.IsInside<string>(joined);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardSeesAnotherValueOrIsOutOfScope()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                void Other(DirectoryInfo root, string leaf, string unrelated)
                {
                    var joined = {|E128222:Path.Combine(root.FullName, leaf)|};
                    Guard.IsInside(unrelated);
                    Guard.IsInside(joined.Length);
                    Guard.IsInside(unrelated + unrelated);
                }

                void Lambda(DirectoryInfo root, string leaf)
                {
                    Func<string> make = () => {|E128222:Path.Combine(root.FullName, leaf)|};
                    Guard.IsInside(make());
                }

                void LocalFunction(DirectoryInfo root, string leaf)
                {
                    string Make() => {|E128222:Path.Combine(root.FullName, leaf)|};
                    Guard.IsInside(Make());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Handles_JoinsInConstructorsAccessorsAndFieldInitializers()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                private static readonly DirectoryInfo Root = new DirectoryInfo(".");

                private static string leaf = "";

                private readonly string field = {|E128222:Path.Combine(Root.FullName, leaf)|};

                private readonly string guarded = Path.Combine(Root.FullName, leaf);

                private string Prop { get; } = Path.Combine(Root.FullName, leaf);

                C()
                {
                    var joined = {|E128222:Path.Combine(Root.FullName, leaf)|};
                }

                string Property
                {
                    get
                    {
                        var joined = {|E128222:Path.Combine(Root.FullName, leaf)|};
                        return joined;
                    }
                }

                void Check()
                {
                    Guard.IsInside(guarded);
                    Guard.IsInside(this.Prop);
                }

                ~C()
                {
                    var joined = {|E128222:Path.Combine(Root.FullName, leaf)|};
                }
            }
            """);
    }
}
