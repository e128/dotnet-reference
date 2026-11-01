using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessStartInfoArgumentCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<ProcessStartInfoArgumentAnalyzer, ProcessStartInfoArgumentCodeFixProvider>(source, fixedCode);

    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected) =>
        CodeFixTestRunner.NoFixAsync<ProcessStartInfoArgumentAnalyzer, ProcessStartInfoArgumentCodeFixProvider>(source, expected: expected);

    [Fact]
    [Trait("Category", "CI")]
    public Task InterpolatedStringArguments_FixedToArgumentListInitializer()
    {
        return VerifyFixAsync(
            """
            using System.Diagnostics;
            class C
            {
                void M(string value)
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "tool",
                        {|E128091:Arguments = $"-x \"{value}\""|}
                    };
                }
            }
            """,
            """
            using System.Diagnostics;
            class C
            {
                void M(string value)
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "tool",
                        ArgumentList =
                        {
                            "-x",
                            value
                        }
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConcatenatedArguments_NoFixOffered()
    {
        return VerifyNoFixAsync(
            """
            using System.Diagnostics;
            class C
            {
                string BuildFlag(string value) => $"-y {value}";

                void M(string value)
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "tool",
                        {|E128091:Arguments = "-x " + BuildFlag(value)|}
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StatementFormAssignment_NoFixOffered()
    {
        // A nested collection initializer is not a statement, so the statement form has no fix.
        return VerifyNoFixAsync(
            """
            using System.Diagnostics;
            class C
            {
                void M(string value)
                {
                    var startInfo = new ProcessStartInfo { FileName = "tool" };
                    {|E128091:startInfo.Arguments = $"-x \"{value}\""|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareIdentifierArguments_NoFixOffered()
    {
        return VerifyNoFixAsync(
            """
            using System.Diagnostics;
            class C
            {
                string BuildArguments(string value) => $"-m \"{value}\"";

                void M(string value)
                {
                    var arguments = BuildArguments(value);
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "tool",
                        {|E128091:Arguments = arguments|}
                    };
                }
            }
            """);
    }
}
