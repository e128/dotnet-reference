using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TextContentGuardAnalyzerTests
{
    private const string AngleSharpStubs = """
                                           namespace AngleSharp.Dom
                                           {
                                               public interface INode { string TextContent { get; } }
                                               public interface IElement : INode { }
                                           }
                                           """;

    private const string NonAngleSharpStubs = """
                                              namespace MyApp
                                              {
                                                  public class Widget { public string TextContent { get; set; } = string.Empty; }
                                              }
                                              """;

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TextContentGuardAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuardBeforeMatch_NoDiagnostic()
    {
        return VerifyAsync($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{AngleSharpStubs}}
                             class C
                             {
                                 void M(IEnumerable<AngleSharp.Dom.IElement> elements)
                                 {
                                     _ = elements.Where(e => e.TextContent.Length < 500 && e.TextContent.Contains("widget")).ToList();
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SmallThreshold_NoDiagnostic()
    {
        return VerifyAsync($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{AngleSharpStubs}}
                             class C
                             {
                                 void M(IEnumerable<AngleSharp.Dom.IElement> elements)
                                 {
                                     _ = elements.Where(e => e.TextContent.Contains("widget") && e.TextContent.Length < 50).ToList();
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonAngleSharp_NoDiagnostic()
    {
        return VerifyAsync($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{NonAngleSharpStubs}}
                             class C
                             {
                                 void M(IEnumerable<MyApp.Widget> widgets)
                                 {
                                     _ = widgets.Where(w => w.TextContent.Contains("widget")).ToList();
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuardAfterMatch_ProducesDiagnostic()
    {
        return VerifyAsync($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{AngleSharpStubs}}
                             class C
                             {
                                 void M(IEnumerable<AngleSharp.Dom.IElement> elements)
                                 {
                                     _ = elements.Where(e => {|E128077:e.TextContent.Contains("widget")|} && e.TextContent.Length < 500).ToList();
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoGuard_ProducesDiagnostic()
    {
        return VerifyAsync($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{AngleSharpStubs}}
                             class C
                             {
                                 void M(IEnumerable<AngleSharp.Dom.IElement> elements)
                                 {
                                     _ = elements.Where(e => {|E128077:e.TextContent.Contains("widget")|}).ToList();
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StartsWith_NoGuard_ProducesDiagnostic()
    {
        return VerifyAsync($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{AngleSharpStubs}}
                             class C
                             {
                                 void M(IEnumerable<AngleSharp.Dom.IElement> elements)
                                 {
                                     _ = elements.Where(e => {|E128077:e.TextContent.StartsWith("Header")|}).ToList();
                                 }
                             }
                             """);
    }
}
