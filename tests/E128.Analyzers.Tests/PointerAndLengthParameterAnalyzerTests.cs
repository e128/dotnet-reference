using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PointerAndLengthParameterAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { AllowUnsafe = true };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAMethodTakesAPointerAndAnInt()
    {
        return AnalyzerTestRunner.VerifyAsync<PointerAndLengthParameterAnalyzer>("""
                           class C
                           {
                               unsafe void Copy(byte* {|E128136:source|}, int length)
                               {
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLengthIsUnsigned()
    {
        return AnalyzerTestRunner.VerifyAsync<PointerAndLengthParameterAnalyzer>("""
                           class C
                           {
                               unsafe void Copy(byte* {|E128136:source|}, uint count)
                               {
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnEveryPointerParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<PointerAndLengthParameterAnalyzer>("""
                           class C
                           {
                               unsafe void Copy(byte* {|E128136:source|}, byte* {|E128136:destination|}, int length)
                               {
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenBothParametersArePointers()
    {
        return AnalyzerTestRunner.VerifyAsync<PointerAndLengthParameterAnalyzer>("""
                           class C
                           {
                               unsafe void Copy(byte* source, byte* destination)
                               {
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBufferIsASpan()
    {
        return AnalyzerTestRunner.VerifyAsync<PointerAndLengthParameterAnalyzer>("""
                           using System;
                           class C
                           {
                               void Copy(ReadOnlySpan<byte> source, int length)
                               {
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePointerHasNoLength()
    {
        return AnalyzerTestRunner.VerifyAsync<PointerAndLengthParameterAnalyzer>("""
                           class C
                           {
                               unsafe void Store(byte* destination)
                               {
                               }
                           }
                           """, options: Options);
    }
}
