using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128059UnusedInterfaceParamAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task UnusedInterfaceParam_Reports_WhenParamIsNotReferenced()
    {
        return AnalyzerTestRunner.VerifyAsync<UnusedInterfaceParamAnalyzer>("""
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
    public Task UnusedInterfaceParam_NoReport_WhenTheParamFollowsTheDiscardConvention()
    {
        // The code fix skips underscore-prefixed parameters, so the analyzer must not report
        // a diagnostic that offers no fix.
        return AnalyzerTestRunner.VerifyAsync<UnusedInterfaceParamAnalyzer>("""
                           interface IProcessor
                           {
                               string Process(string _input);
                           }
                           class Processor : IProcessor
                           {
                               public string Process(string _input)
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
        return AnalyzerTestRunner.VerifyAsync<UnusedInterfaceParamAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<UnusedInterfaceParamAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<UnusedInterfaceParamAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<UnusedInterfaceParamAnalyzer>("""
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
