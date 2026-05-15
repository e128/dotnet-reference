using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class QuerySelectorAllMaterializationAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<QuerySelectorAllMaterializationAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task QuerySelectorAll_DirectInForeach_Fires()
    {
        return VerifyAsync("""
                           using AngleSharp.Dom;
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task QuerySelectorAll_WithLinqNoToList_Fires()
    {
        return VerifyAsync("""
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
                                   foreach (var el in {|E128076:doc.QuerySelectorAll("p")|}.Where(e => e != null))
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task QuerySelectorAll_ToList_NoFire()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task QuerySelectorAll_ToArray_NoFire()
    {
        return VerifyAsync("""
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
                                   foreach (var el in doc.QuerySelectorAll("p").ToArray())
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonAngleSharp_QuerySelectorAll_NoFire()
    {
        return VerifyAsync("""
                           namespace MyApp
                           {
                               public class MyDocument
                               {
                                   public System.Collections.Generic.IEnumerable<object> QuerySelectorAll(string selector)
                                       => System.Array.Empty<object>();
                               }
                           }
                           class C
                           {
                               void M(MyApp.MyDocument doc)
                               {
                                   foreach (var el in doc.QuerySelectorAll("p"))
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForEach_OverVariable_AssignedFromQSA_WithoutToList_Reports()
    {
        return VerifyAsync("""
                           using AngleSharp.Dom;
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
                                   var items = {|E128076:doc.QuerySelectorAll("div")|};
                                   foreach (var el in items)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForEach_OverVariable_AssignedFromQSA_WithToList_NoDiagnostic()
    {
        return VerifyAsync("""
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
                                   var items = doc.QuerySelectorAll("div").ToList();
                                   foreach (var el in items)
                                   {
                                   }
                               }
                           }
                           """);
    }
}
