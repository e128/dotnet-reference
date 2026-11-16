using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedBufferFieldAnalyzerBranchCoverageMoreTests
{
    private const string Buffer = """
        unsafe struct Buffer
        {
            public fixed byte Data[16];
        }
        """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLanguageVersionPredatesInlineArrays()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedBufferFieldAnalyzer>(
            Buffer,
            options: new AnalyzerTestOptions { AllowUnsafe = true, LanguageVersion = LanguageVersion.CSharp11 });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRuntimeHasNoInlineArrayAttribute()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedBufferFieldAnalyzer>(
            Buffer,
            options: new AnalyzerTestOptions
            {
                AllowUnsafe = true,
                References = ReferenceAssemblies.NetStandard.NetStandard20,
                CompilerDiagnostics = CompilerDiagnostics.None
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFixedBufferHasNoLength()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedBufferFieldAnalyzer>(
            """
            unsafe struct Buffer
            {
                public fixed byte Data[0];
            }
            """,
            options: new AnalyzerTestOptions { AllowUnsafe = true, CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
