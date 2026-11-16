using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnusedInterfaceParamCodeFixProviderBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PrefixesAnUnusedParameterWithAnUnderscore()
    {
        return CodeFixTestRunner.RunAsync<UnusedInterfaceParamAnalyzer, UnusedInterfaceParamCodeFixProvider>(
            """
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
            """,
            """
            interface IProcessor
            {
                string Process(string input);
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
    public Task FixAll_RenamesEveryUnusedParameterInTheDocument()
    {
        return CodeFixTestRunner.RunAsync<UnusedInterfaceParamAnalyzer, UnusedInterfaceParamCodeFixProvider>(
            """
            interface IProcessor
            {
                string Process(string input, int count);
            }

            class Processor : IProcessor
            {
                public string Process({|E128059:string input|}, {|E128059:int count|})
                {
                    return "constant";
                }
            }
            """,
            """
            interface IProcessor
            {
                string Process(string input, int count);
            }

            class Processor : IProcessor
            {
                public string Process(string _input, int _count)
                {
                    return "constant";
                }
            }
            """);
    }
}
