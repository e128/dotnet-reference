using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DllImportToLibraryImportAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { CompilerDiagnostics = CompilerDiagnostics.None };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenDllImportIsUsed()
    {
        return AnalyzerTestRunner.VerifyAsync<DllImportToLibraryImportAnalyzer>("""
                           using System.Runtime.InteropServices;
                           static partial class Native
                           {
                               [{|E128117:DllImport("libc")|}]
                               internal static extern int Abs(int value);
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenLibraryImportIsUsed()
    {
        return AnalyzerTestRunner.VerifyAsync<DllImportToLibraryImportAnalyzer>("""
                           using System.Runtime.InteropServices;
                           static partial class Native
                           {
                               [LibraryImport("libc")]
                               internal static partial int Abs(int value);
                           }
                           """, options: Options);
    }
}
