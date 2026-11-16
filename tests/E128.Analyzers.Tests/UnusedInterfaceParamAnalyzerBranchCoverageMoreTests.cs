using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnusedInterfaceParamAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheImplementationHasNoParameters()
    {
        return AnalyzerTestRunner.VerifyAsync<UnusedInterfaceParamAnalyzer>("""
            interface IProcessor
            {
                string Process();
            }
            class Processor : IProcessor
            {
                public string Process()
                {
                    return "constant";
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnExpressionBodiedImplementation()
    {
        return AnalyzerTestRunner.VerifyAsync<UnusedInterfaceParamAnalyzer>("""
            interface IProcessor
            {
                string Process(string input);
            }
            class Processor : IProcessor
            {
                public string Process({|E128059:string input|}) => "constant";
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnAbstractImplementationWithoutABody()
    {
        return AnalyzerTestRunner.VerifyAsync<UnusedInterfaceParamAnalyzer>("""
            interface IProcessor
            {
                string Process(string input);
            }
            abstract class Processor : IProcessor
            {
                public abstract string Process(string input);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForConstructorsAndRecordMembers()
    {
        return AnalyzerTestRunner.VerifyAsync<UnusedInterfaceParamAnalyzer>("""
            interface IMarker
            {
            }
            record Person(string Name) : IMarker;
            class Holder : IMarker
            {
                public Holder(int count)
                {
                }
            }
            """);
    }
}
