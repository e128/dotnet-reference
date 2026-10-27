using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DllImportToLibraryImportCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        // The fixed code needs the LibraryImport source generator, which the test
        // harness does not run. Compiler diagnostics are therefore not validated.
        var test = new CSharpCodeFixTest<
            DllImportToLibraryImportAnalyzer,
            DllImportToLibraryImportCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.SolutionTransforms.Add((solution, projectId) =>
            solution.GetProject(projectId)?.CompilationOptions is CSharpCompilationOptions options
                ? solution.WithProjectCompilationOptions(projectId, options.WithAllowUnsafe(true))
                : solution);

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesTheAttributeAndMakesTheMethodPartial()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.InteropServices;
            static class Native
            {
                [{|E128117:DllImport("libc", EntryPoint = "abs")|}]
                internal static extern int Abs(int value);
            }
            """,
            """
            using System.Runtime.InteropServices;
            static partial class Native
            {
                [LibraryImport("libc", EntryPoint = "abs")]
                internal static partial int Abs(int value);
            }
            """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheAttributeUsesAnArgumentLibraryImportLacks()
    {
        const string Code = """
            using System.Runtime.InteropServices;
            static class Native
            {
                [{|E128117:DllImport("libc", CallingConvention = CallingConvention.Cdecl)|}]
                internal static extern int Abs(int value);
            }
            """;

        return VerifyFixAsync(Code, Code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheSignatureUsesChar()
    {
        const string Code = """
            using System.Runtime.InteropServices;
            static class Native
            {
                [{|E128117:DllImport("libc")|}]
                internal static extern char Next(char value);
            }
            """;

        return VerifyFixAsync(Code, Code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsAQualifiedAttributeName()
    {
        return VerifyFixAsync(
            """
            static class Native
            {
                [{|E128117:System.Runtime.InteropServices.DllImport("libc")|}]
                internal static extern int Abs(int value);
            }
            """,
            """
            static partial class Native
            {
                [System.Runtime.InteropServices.LibraryImport("libc")]
                internal static partial int Abs(int value);
            }
            """);
    }
}
