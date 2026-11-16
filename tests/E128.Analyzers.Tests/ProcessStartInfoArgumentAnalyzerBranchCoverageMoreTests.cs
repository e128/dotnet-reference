using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessStartInfoArgumentAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code, bool lenient = false)
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessStartInfoArgumentAnalyzer>(
            code,
            options: lenient ? new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None } : null);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsANamespace()
    {
        return VerifyAsync(
            """
            class C
            {
                void M(string value)
                {
                    System.Diagnostics.Arguments = value;
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenArgumentListIsAssignedAfterTheArguments()
    {
        return VerifyAsync(
            """
            using System.Diagnostics;
            class C
            {
                void M(string value)
                {
                    var info = new ProcessStartInfo("tool");
                    info.Arguments = value;
                    info.ArgumentList = null;
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOtherAssignmentsSurroundTheArguments()
    {
        return VerifyAsync("""
            using System.Diagnostics;
            class C
            {
                void M(string value)
                {
                    var info = new ProcessStartInfo("tool");
                    var other = new ProcessStartInfo("tool");
                    int count;
                    count = 1;
                    other.FileName = "x";
                    {|E128091:info.Arguments = value|};
                    other.ArgumentList.Add("x");
                    var unused = count;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherReceiverGetsTheArgumentList()
    {
        return VerifyAsync(
            """
            using System.Diagnostics;
            class C
            {
                void M(string value)
                {
                    var info = new ProcessStartInfo("tool");
                    var other = new ProcessStartInfo("tool");
                    {|E128091:info.Arguments = value|};
                    other.ArgumentList = null;
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheInitializerAlsoHoldsAnIndexerAssignment()
    {
        return VerifyAsync(
            """
            using System.Diagnostics;
            class C
            {
                void M(string value)
                {
                    var info = new ProcessStartInfo { [0] = 1, {|E128091:Arguments = value|} };
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForNonEmptyConstantsAndValuesThatAreNotStringEmpty()
    {
        return VerifyAsync("""
            using System.Diagnostics;
            class Holder
            {
                public static string Empty = "";

                public string Name { get; set; } = "";
            }

            class C
            {
                void M(Holder holder)
                {
                    var a = new ProcessStartInfo { {|E128091:Arguments = "-v"|} };
                    var b = new ProcessStartInfo { {|E128091:Arguments = Holder.Empty|} };
                    var c = new ProcessStartInfo { {|E128091:Arguments = holder.Name|} };
                }
            }
            """);
    }
}
