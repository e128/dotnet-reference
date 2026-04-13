using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128057FinallyBlockCleanupAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FinallyBlockCleanupAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FinallyCleanup_Reports_WhenFileDeleteIsNotInTryCatch()
    {
        return VerifyAsync("""
            using System.IO;
            class Processor
            {
                void Process(string tempPath)
                {
                    try { }
                    finally
                    {
                        {|E128057:File.Delete(tempPath)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FinallyCleanup_NoReport_WhenFileDeleteIsInTryCatch()
    {
        return VerifyAsync("""
            using System.IO;
            class Processor
            {
                void Process(string tempPath)
                {
                    try { }
                    finally
                    {
                        try
                        {
                            File.Delete(tempPath);
                        }
                        catch (IOException) { }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FinallyCleanup_Reports_WhenDirectoryDeleteIsNotInTryCatch()
    {
        return VerifyAsync("""
            using System.IO;
            class Processor
            {
                void Process(string tempPath)
                {
                    try { }
                    finally
                    {
                        {|E128057:Directory.Delete(tempPath, true)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FinallyCleanup_NoReport_WhenFinallyHasNoDangerousCall()
    {
        return VerifyAsync("""
            class Processor
            {
                void Process()
                {
                    try { }
                    finally
                    {
                        var x = 1;
                    }
                }
            }
            """);
    }
}
