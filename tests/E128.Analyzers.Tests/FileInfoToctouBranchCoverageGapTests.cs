using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileInfoToctouBranchCoverageGapTests
{
    private static readonly AnalyzerTestOptions NoCompilerDiagnostics = new() { CompilerDiagnostics = CompilerDiagnostics.None };

    private static Task VerifyAsync(string members, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>(
            "using System;\nusing System.IO;\nclass FieldMarker { public bool Exists; }\nclass C\n{\n" + members + "}\n",
            expected: null,
            options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReadArgumentIsTheResultOfACall()
    {
        return VerifyAsync("""
                static string Name() => "x";

                string M(string path)
                {
                    if (!File.Exists(path)) return string.Empty;
                    return File.ReadAllText(Name());
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheExistsMemberIsAField()
    {
        return VerifyAsync("""
                string M(FieldMarker marker, string path)
                {
                    if (!marker.Exists) return string.Empty;
                    return File.ReadAllText(path);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardedArgumentCannotBeBound()
    {
        return VerifyAsync(
            """
                string M(string path)
                {
                    if (!File.Exists(path) || !File.Exists(missing)) return string.Empty;
                    return File.ReadAllText(missing);
                }

            """,
            NoCompilerDiagnostics);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenFileCallsCarryNoArguments()
    {
        return VerifyAsync(
            """
                string M()
                {
                    if (!File.Exists()) return string.Empty;
                    return File.ReadAllText();
                }

            """,
            NoCompilerDiagnostics);
    }
}
