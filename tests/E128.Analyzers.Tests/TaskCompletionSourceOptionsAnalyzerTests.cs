using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskCompletionSourceOptionsAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TaskCompletionSourceOptionsAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoOptionsArgument_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return {|E128159:new TaskCompletionSource<int>()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EmptyOptionsArgument_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return {|E128159:new TaskCompletionSource<int>(TaskCreationOptions.None)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FlagPresent_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComputedOptions_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   TaskCreationOptions options = TaskCreationOptions.None;
                                   return new TaskCompletionSource<int>(options);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnrelatedCreation_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               object M()
                               {
                                   return new List<int>();
                               }
                           }
                           """);
    }
}
