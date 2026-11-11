using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RawIlEmitAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCodeEmitsAnOpCode()
    {
        return AnalyzerTestRunner.VerifyAsync<RawIlEmitAnalyzer>("""
                           using System.Reflection.Emit;
                           class C
                           {
                               void M(ILGenerator generator)
                               {
                                   {|E128138:generator.Emit(OpCodes.Nop)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCodeEmitsAnInstruction()
    {
        return AnalyzerTestRunner.VerifyAsync<RawIlEmitAnalyzer>("""
                           using System.Reflection.Emit;
                           class C
                           {
                               void M(ILGenerator generator)
                               {
                                   {|E128138:generator.Emit(OpCodes.Ldc_I4_1)|};
                                   {|E128138:generator.Emit(OpCodes.Pop)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsNotAnIlGenerator()
    {
        return AnalyzerTestRunner.VerifyAsync<RawIlEmitAnalyzer>("""
                           class Emitter
                           {
                               public void Emit(int value)
                               {
                               }
                           }
                           class C
                           {
                               void M(Emitter emitter)
                               {
                                   emitter.Emit(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMethodOnlyReceivesAGenerator()
    {
        return AnalyzerTestRunner.VerifyAsync<RawIlEmitAnalyzer>("""
                           using System.Reflection.Emit;
                           class C
                           {
                               void M(ILGenerator generator)
                               {
                               }
                           }
                           """);
    }
}
