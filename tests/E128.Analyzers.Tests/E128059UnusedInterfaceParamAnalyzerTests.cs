using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128059UnusedInterfaceParamAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<UnusedInterfaceParamAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnusedInterfaceParam_Reports_WhenParamIsNotReferenced()
    {
        return VerifyAsync("""
                           interface IProcessor
                           {
                               string Process(string input);
                           }
                           class Processor : IProcessor
                           {
                               public string Process({|E128059:string input|})
                               {
                                   return "constant";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnusedInterfaceParam_NoReport_WhenAllParamsAreUsed()
    {
        return VerifyAsync("""
                           interface IProcessor
                           {
                               string Process(string input);
                           }
                           class Processor : IProcessor
                           {
                               public string Process(string input)
                               {
                                   return input.ToUpper();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnusedInterfaceParam_NoReport_WhenCancellationTokenIsUnused()
    {
        return VerifyAsync("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           interface IWorker
                           {
                               Task DoWorkAsync(CancellationToken ct);
                           }
                           class Worker : IWorker
                           {
                               public Task DoWorkAsync(CancellationToken ct)
                               {
                                   return Task.CompletedTask;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnusedInterfaceParam_NoReport_WhenMethodIsNotInterfaceImplementation()
    {
        return VerifyAsync("""
                           class Utility
                           {
                               public string Process(string input)
                               {
                                   return "constant";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnusedInterfaceParam_Reports_OnlyUnusedParams_WhenSomeParamsUsed()
    {
        return VerifyAsync("""
                           interface ITransformer
                           {
                               string Transform(string input, string prefix);
                           }
                           class Transformer : ITransformer
                           {
                               public string Transform(string input, {|E128059:string prefix|})
                               {
                                   return input.ToUpper();
                               }
                           }
                           """);
    }
}
