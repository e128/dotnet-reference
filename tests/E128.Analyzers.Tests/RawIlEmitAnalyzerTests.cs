using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RawIlEmitAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<RawIlEmitAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCodeEmitsAnOpCode()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
