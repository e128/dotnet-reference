using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessStartInfoArgumentCodeFixBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ProcessStartInfoArgumentAnalyzer, ProcessStartInfoArgumentCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<ProcessStartInfoArgumentAnalyzer, ProcessStartInfoArgumentCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralArguments_SplitIntoOneTokenPerWord()
    {
        return VerifyFixAsync(
            """
            using System.Diagnostics;
            class C
            {
                void M()
                {
                    var startInfo = new ProcessStartInfo
                    {
                        {|E128091:Arguments = "-a  --long \"two words\" tail"|}
                    };
                }
            }
            """,
            """
            using System.Diagnostics;
            class C
            {
                void M()
                {
                    var startInfo = new ProcessStartInfo
                    {
                        ArgumentList =
                        {
                            "-a",
                            "--long",
                            "two words",
                            "tail"
                        }
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterpolatedArguments_KeepAHoleGluedToItsTextAsOneToken()
    {
        return VerifyFixAsync(
            """
            using System.Diagnostics;
            class C
            {
                void M(string name, string other)
                {
                    var startInfo = new ProcessStartInfo
                    {
                        {|E128091:Arguments = $"--name={name} {other} {name}{other}"|}
                    };
                }
            }
            """,
            """
            using System.Diagnostics;
            class C
            {
                void M(string name, string other)
                {
                    var startInfo = new ProcessStartInfo
                    {
                        ArgumentList =
                        {
                            $"--name={name}",
                            other,
                            $"{name}{other}"
                        }
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheQuotesAreUnbalancedOrTheTextIsOnlyWhitespace()
    {
        return VerifyNoFixAsync(
            """
            using System.Diagnostics;
            class C
            {
                void M(string name)
                {
                    var a = new ProcessStartInfo
                    {
                        {|E128091:Arguments = $"--name=\"{name}"|}
                    };
                    var b = new ProcessStartInfo
                    {
                        {|E128091:Arguments = "   "|}
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAHoleHasAFormatOrAlignmentClause()
    {
        return VerifyNoFixAsync(
            """
            using System.Diagnostics;
            class C
            {
                void M(int count)
                {
                    var a = new ProcessStartInfo
                    {
                        {|E128091:Arguments = $"-n {count:x}"|}
                    };
                    var b = new ProcessStartInfo
                    {
                        {|E128091:Arguments = $"-n {count,5}"|}
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheArgumentsAreAssignedOutsideAnObjectInitializer()
    {
        return VerifyNoFixAsync(
            """
            using System.Diagnostics;
            class C
            {
                void M(string name)
                {
                    var startInfo = new ProcessStartInfo("tool");
                    {|E128091:startInfo.Arguments = $"-x {name}"|};
                }
            }
            """);
    }
}
