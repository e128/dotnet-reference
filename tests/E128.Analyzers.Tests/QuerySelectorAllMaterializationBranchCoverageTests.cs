using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class QuerySelectorAllMaterializationBranchCoverageTests
{
    private const string Stub = """
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
                                    public abstract class Page
                                    {
                                        protected IEnumerable<IElement> QuerySelectorAll(string selector) => new List<IElement>();
                                    }
                                }
                                """;

    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<QuerySelectorAllMaterializationAnalyzer>(Stub + "\nclass C\n{\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALinqChainEndsInToList()
    {
        return VerifyAsync("""
                void M(IDocument doc)
                {
                    foreach (var element in doc.QuerySelectorAll("p").Where(e => e != null).ToList())
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheQueryRunsInsideALambdaOfTheInitializer()
    {
        return VerifyAsync("""
                void M(IDocument doc)
                {
                    var items = new[] { 1 }.SelectMany(_ => doc.QuerySelectorAll("p"));
                    foreach (var element in items)
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopVariableHasNoInitializer()
    {
        return VerifyAsync("""
                void M(IDocument doc)
                {
                    IEnumerable<IElement> items;
                    items = doc.QuerySelectorAll("p");
                    foreach (var element in items)
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopSourceIsNotALocal()
    {
        return VerifyAsync("""
                IEnumerable<IElement> field = new List<IElement>();

                void M(IEnumerable<IElement> parameter)
                {
                    foreach (var element in parameter)
                    {
                    }

                    foreach (var element in field)
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnUnqualifiedQueryOnAnAngleSharpType()
    {
        return AnalyzerTestRunner.VerifyAsync<QuerySelectorAllMaterializationAnalyzer>(
            Stub + """

            class Child : Page
            {
                void M()
                {
                    foreach (var element in {|E128076:QuerySelectorAll("p")|})
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnInitializerThatQueriesBeforeAnotherCall()
    {
        return VerifyAsync("""
                void M(IDocument doc)
                {
                    var items = {|E128076:doc.QuerySelectorAll("p")|}.Where(e => e != null);
                    foreach (var element in items)
                    {
                    }
                }

            """);
    }
}
