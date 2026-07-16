using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessStartInfoArgumentCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<ProcessStartInfoArgumentAnalyzer, ProcessStartInfoArgumentCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<ProcessStartInfoArgumentAnalyzer, ProcessStartInfoArgumentCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllInDocumentIterations = 0
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

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
