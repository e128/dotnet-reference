using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FunctionPointerTypeAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<FunctionPointerTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.SolutionTransforms.Add(CodeFixTestRunner.AllowUnsafe);

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnAField()
    {
        return VerifyAsync("""
                           unsafe class C
                           {
                               {|E128200:delegate*<int, int>|} field;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnAnUnmanagedPointer()
    {
        return VerifyAsync("""
                           unsafe class C
                           {
                               {|E128200:delegate* unmanaged[Cdecl]<int, void>|} field;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnALocal()
    {
        return VerifyAsync("""
                           unsafe class C
                           {
                               void M()
                               {
                                   {|E128200:delegate*<int, int>|} f = null;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsADelegate()
    {
        return VerifyAsync("""
                           class C
                           {
                               System.Func<int, int> field;
                           }
                           """);
    }
}
