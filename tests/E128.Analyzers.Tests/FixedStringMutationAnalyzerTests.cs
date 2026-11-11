using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedStringMutationAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { AllowUnsafe = true };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAStringIsPinnedAsAWritablePointer()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedStringMutationAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnArrayIsPinned()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedStringMutationAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStringIsOnlyRead()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedStringMutationAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWriteUsesPointerArithmetic()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedStringMutationAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyThePointerItselfMoves()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedStringMutationAnalyzer>("""
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
                           """, options: Options);
    }
}
