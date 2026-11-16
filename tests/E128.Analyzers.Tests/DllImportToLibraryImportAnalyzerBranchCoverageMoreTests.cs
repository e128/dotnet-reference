using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DllImportToLibraryImportAnalyzerBranchCoverageMoreTests
{
    private static AnalyzerTestOptions Lenient => new() { CompilerDiagnostics = CompilerDiagnostics.None };

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
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
        test.SolutionTransforms.Add(CodeFixTestRunner.AllowUnsafe);

        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRuntimeLacksLibraryImportAttribute()
    {
        return AnalyzerTestRunner.VerifyAsync<DllImportToLibraryImportAnalyzer>(
            """
            using System.Runtime.InteropServices;
            static class Native
            {
                [DllImport("libc")]
                internal static extern int Abs(int value);
            }
            """,
            options: new AnalyzerTestOptions
            {
                References = ReferenceAssemblies.NetStandard.NetStandard20,
                CompilerDiagnostics = CompilerDiagnostics.None
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheDllImportAttributeDoesNotResolve()
    {
        return AnalyzerTestRunner.VerifyAsync<DllImportToLibraryImportAnalyzer>(
            """
            static class Native
            {
                [DllImport("libc")]
                internal static extern int Abs(int value);
            }
            """,
            options: Lenient);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAUserAttributeIsNamedDllImport()
    {
        return AnalyzerTestRunner.VerifyAsync<DllImportToLibraryImportAnalyzer>(
            """
            using System;
            class DllImport : Attribute
            {
                public DllImport(string name) { }
            }
            static class Native
            {
                [DllImport("libc")]
                internal static int Abs(int value) => value;
            }
            """,
            options: Lenient);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAttributeNameIsAliasQualified()
    {
        return AnalyzerTestRunner.VerifyAsync<DllImportToLibraryImportAnalyzer>(
            """
            using System;
            class DllImportAttribute : Attribute
            {
                public DllImportAttribute(string name) { }
            }
            static class Native
            {
                [global::DllImport("libc")]
                internal static int Abs(int value) => value;
            }
            """,
            options: Lenient);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AcceptsAVoidReturn()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.InteropServices;
            static class Native
            {
                [{|E128117:DllImport("libc")|}]
                internal static extern void Sleep(int milliseconds);
            }
            """,
            """
            using System.Runtime.InteropServices;
            static partial class Native
            {
                [LibraryImport("libc")]
                internal static partial void Sleep(int milliseconds);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AcceptsEnumParametersAndReturns()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.InteropServices;
            enum Mode : int { A, B }
            static class Native
            {
                [{|E128117:DllImport("libc")|}]
                internal static extern Mode Next(Mode mode);
            }
            """,
            """
            using System.Runtime.InteropServices;
            enum Mode : int { A, B }
            static partial class Native
            {
                [LibraryImport("libc")]
                internal static partial Mode Next(Mode mode);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AcceptsPointerParametersAndReturns()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.InteropServices;
            static class Native
            {
                [{|E128117:DllImport("libc")|}]
                internal static extern unsafe int* Next(int* cursor);
            }
            """,
            """
            using System.Runtime.InteropServices;
            static partial class Native
            {
                [LibraryImport("libc")]
                internal static unsafe partial int* Next(int* cursor);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenOnlyTheReturnTypeIsNotBlittable()
    {
        const string Code = """
            using System.Runtime.InteropServices;
            static class Native
            {
                [{|E128117:DllImport("libc")|}]
                internal static extern string Name(int value);
            }
            """;

        return VerifyFixAsync(Code, Code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheQualifiedAttributeNameEndsInAGenericName()
    {
        return AnalyzerTestRunner.VerifyAsync<DllImportToLibraryImportAnalyzer>(
            """
            static class Native
            {
                [System.Runtime.InteropServices.DllImport<int>("libc")]
                internal static extern int Abs(int value);
            }
            """,
            options: Lenient);
    }
}
