using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class QuerySelectorAllMaterializationAnalyzerBranchCoverageMoreTests
{
    private const string Stub = """
                                using System;
                                using System.Collections.Generic;
                                using System.Linq;
                                using AngleSharp.Dom;
                                namespace AngleSharp.Dom
                                {
                                    public interface IElement { }
                                    public interface IDocument
                                    {
                                        IEnumerable<IElement> QuerySelectorAll(string selector);
                                    }
                                }
                                class Holder
                                {
                                    public IEnumerable<IElement> Elements { get; } = new List<IElement>();
                                }
                                class Own
                                {
                                    public IEnumerable<int> QuerySelectorAll(string selector) => new List<int>();
                                }
                                """;

    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<QuerySelectorAllMaterializationAnalyzer>(
            Stub + "\nclass C\n{\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopSourceIsAnIterationVariable()
    {
        return VerifyAsync("""
                void M(IEnumerable<IEnumerable<IElement>> groups)
                {
                    foreach (var group in groups)
                    {
                        foreach (var element in group)
                        {
                        }
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheQueryReceiverIsDynamic()
    {
        return VerifyAsync("""
                void M(dynamic doc)
                {
                    foreach (var element in doc.QuerySelectorAll("p"))
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopSourceIsAnInvokedDelegateResult()
    {
        return VerifyAsync("""
                void M(Func<Func<IEnumerable<IElement>>> factory)
                {
                    foreach (var element in factory()())
                    {
                    }
                }

            """);
    }


    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheQueryIsAnArgumentOfAnotherCall()
    {
        return VerifyAsync("""
                static IEnumerable<IElement> Wrap(IEnumerable<IElement> elements) => elements;

                void M(IDocument doc)
                {
                    foreach (var element in Wrap({|E128076:doc.QuerySelectorAll("p")|}))
                    {
                    }
                }

            """);
    }
}
