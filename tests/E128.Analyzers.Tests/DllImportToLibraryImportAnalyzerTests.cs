using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DllImportToLibraryImportAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        // A [LibraryImport] partial method needs its source generator, which the
        // test harness does not run. Compiler diagnostics are therefore not validated.
        return new CSharpAnalyzerTest<DllImportToLibraryImportAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenDllImportIsUsed()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           static partial class Native
                           {
                               [{|E128117:DllImport("libc")|}]
                               internal static extern int Abs(int value);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenLibraryImportIsUsed()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           static partial class Native
                           {
                               [LibraryImport("libc")]
                               internal static partial int Abs(int value);
                           }
                           """);
    }
}
