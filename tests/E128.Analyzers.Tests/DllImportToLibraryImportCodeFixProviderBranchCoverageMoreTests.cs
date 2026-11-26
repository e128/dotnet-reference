using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DllImportToLibraryImportCodeFixProviderBranchCoverageMoreTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        // The fixed code needs the LibraryImport source generator, which the test
        // harness does not run. Compiler diagnostics are therefore not validated.
        return CodeFixTestRunner.RunAsync<DllImportToLibraryImportAnalyzer, DllImportToLibraryImportCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsATypeThatIsAlreadyPartial()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.InteropServices;
            static partial class Native
            {
                [{|E128117:DllImport("libc")|}]
                internal static extern int Abs(int value);
            }
            """,
            """
            using System.Runtime.InteropServices;
            static partial class Native
            {
                [LibraryImport("libc")]
                internal static partial int Abs(int value);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsAMethodThatIsAlreadyPartial()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.InteropServices;
            static class Native
            {
                [{|E128117:DllImport("libc")|}]
                internal static extern partial int Abs(int value);
            }
            """,
            """
            using System.Runtime.InteropServices;
            static partial class Native
            {
                [LibraryImport("libc")]
                internal static partial int Abs(int value);
            }
            """);
    }
}
