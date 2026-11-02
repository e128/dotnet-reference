using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedStringMutationAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<FixedStringMutationAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.SolutionTransforms.Add(CodeFixTestRunner.AllowUnsafe);

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAStringIsPinnedAsAWritablePointer()
    {
        return VerifyAsync("""
                           unsafe class C
                           {
                               void M(string text)
                               {
                                   {|E128126:fixed (char* pointer = text)
                                   {
                                       pointer[0] = 'a';
                                   }|}
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnArrayIsPinned()
    {
        return VerifyAsync("""
                           unsafe class C
                           {
                               void M(char[] buffer)
                               {
                                   fixed (char* pointer = buffer)
                                   {
                                       pointer[0] = 'a';
                                   }
                               }
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStringIsOnlyRead()
    {
        return VerifyAsync("""
                           unsafe class C
                           {
                               int M(string text)
                               {
                                   fixed (char* pointer = text)
                                   {
                                       return pointer[0] + *(pointer + 1);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWriteUsesPointerArithmetic()
    {
        return VerifyAsync("""
                           unsafe class C
                           {
                               void M(string text)
                               {
                                   {|E128126:fixed (char* pointer = text)
                                   {
                                       *(pointer + 1) = 'a';
                                   }|}
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyThePointerItselfMoves()
    {
        return VerifyAsync("""
                           unsafe class C
                           {
                               void M(string text)
                               {
                                   fixed (char* pointer = text)
                                   {
                                       var cursor = pointer;
                                       cursor++;
                                   }
                               }
                           }
                           """);
    }
}
