using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class QuerySelectorAllMaterializationCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<QuerySelectorAllMaterializationAnalyzer, QuerySelectorAllMaterializationCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task QuerySelectorAll_DirectForeach_FixAddsToList()
    {
        const string source = """
                              using AngleSharp.Dom;
                              using System.Linq;
                              namespace AngleSharp.Dom
                              {
                                  public interface IElement { }
                                  public interface IDocument
                                  {
                                      System.Collections.Generic.IEnumerable<IElement> QuerySelectorAll(string selector);
                                  }
                              }
                              class C
                              {
                                  void M(IDocument doc)
                                  {
                                      foreach (var el in {|E128076:doc.QuerySelectorAll("p")|})
                                      {
                                      }
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using AngleSharp.Dom;
                                 using System.Linq;
                                 namespace AngleSharp.Dom
                                 {
                                     public interface IElement { }
                                     public interface IDocument
                                     {
                                         System.Collections.Generic.IEnumerable<IElement> QuerySelectorAll(string selector);
                                     }
                                 }
                                 class C
                                 {
                                     void M(IDocument doc)
                                     {
                                         foreach (var el in doc.QuerySelectorAll("p").ToList())
                                         {
                                         }
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
