using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PointerAndLengthParameterAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<PointerAndLengthParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectCompilationOptions(
            projectId,
            ((CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true)));

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAMethodTakesAPointerAndAnInt()
    {
        return VerifyAsync("""
                           class C
                           {
                               unsafe void Copy(byte* {|E128136:source|}, int length)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLengthIsUnsigned()
    {
        return VerifyAsync("""
                           class C
                           {
                               unsafe void Copy(byte* {|E128136:source|}, uint count)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnEveryPointerParameter()
    {
        return VerifyAsync("""
                           class C
                           {
                               unsafe void Copy(byte* {|E128136:source|}, byte* {|E128136:destination|}, int length)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenBothParametersArePointers()
    {
        return VerifyAsync("""
                           class C
                           {
                               unsafe void Copy(byte* source, byte* destination)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBufferIsASpan()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void Copy(ReadOnlySpan<byte> source, int length)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePointerHasNoLength()
    {
        return VerifyAsync("""
                           class C
                           {
                               unsafe void Store(byte* destination)
                               {
                               }
                           }
                           """);
    }
}
