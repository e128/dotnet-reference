using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessStartInfoArgumentBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessStartInfoArgumentAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_TargetTypedCreationAndStatementAssignments()
    {
        return VerifyAsync("""
            using System.Diagnostics;
            class C
            {
                void M(string value)
                {
                    ProcessStartInfo a = new() { {|E128091:Arguments = value|} };
                    var b = new ProcessStartInfo("tool");
                    {|E128091:b.Arguments = value|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueIsEmptyOrArgumentListIsUsed()
    {
        return VerifyAsync("""
            using System.Diagnostics;
            class C
            {
                const string None = "";

                void M(string value)
                {
                    var a = new ProcessStartInfo { Arguments = "" };
                    var b = new ProcessStartInfo { Arguments = string.Empty };
                    var c = new ProcessStartInfo { Arguments = None };
                    var d = new ProcessStartInfo { Arguments = value, ArgumentList = { "x" } };
                    var e = new ProcessStartInfo();
                    var f = new ProcessStartInfo { FileName = "tool" };
                    a.Arguments = "";
                    var g = new System.Text.StringBuilder();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsNotAProcessStartInfoOrNotAMemberAccess()
    {
        return VerifyAsync("""
            using System.Diagnostics;

            namespace Other
            {
                public sealed class ProcessStartInfo
                {
                    public string Arguments { get; set; } = "";
                }
            }

            class Holder
            {
                public string Arguments { get; set; } = "";

                public string Name { get; set; } = "";
            }

            class C
            {
                void M(string value, Holder holder, Other.ProcessStartInfo other)
                {
                    holder.Arguments = value;
                    holder.Name = value;
                    other.Arguments = value;
                    var created = new Other.ProcessStartInfo { Arguments = value };
                    var array = new string[1];
                    array[0] = value;
                }
            }
            """);
    }
}
