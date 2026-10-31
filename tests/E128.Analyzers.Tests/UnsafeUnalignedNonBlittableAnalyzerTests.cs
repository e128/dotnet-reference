using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeUnalignedNonBlittableAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<UnsafeUnalignedNonBlittableAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            return solution.GetProject(projectId)?.CompilationOptions is CSharpCompilationOptions options
                ? solution.WithProjectCompilationOptions(projectId, options.WithAllowUnsafe(true))
                : solution;
        });

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenReadingABool()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               bool M(ref byte source)
                               {
                                   return {|E128139:Unsafe.ReadUnaligned<bool>(ref source)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenReadingABoolFromAnInArgument()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               bool M(ref readonly byte source)
                               {
                                   return {|E128139:Unsafe.ReadUnaligned<bool>(in source)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenWritingABool()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(ref byte destination, bool value)
                               {
                                   {|E128139:Unsafe.WriteUnaligned<bool>(ref destination, value)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStructHoldsABool()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           struct Flag
                           {
                               public bool Value;
                           }
                           unsafe class C
                           {
                               Flag M(ref byte source)
                               {
                                   return {|E128139:Unsafe.ReadUnaligned<Flag>(ref source)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsBlittable()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               int M(ref byte source)
                               {
                                   return Unsafe.ReadUnaligned<int>(ref source);
                               }
                           }
                           """);
    }
}
