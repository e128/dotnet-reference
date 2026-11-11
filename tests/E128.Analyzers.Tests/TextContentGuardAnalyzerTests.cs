using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TextContentGuardAnalyzerTests
{
    private const string NonAngleSharpStubs = """
                                              namespace MyApp
                                              {
                                                  public class Widget { public string TextContent { get; set; } = string.Empty; }
                                              }
                                              """;

    [Fact]
    [Trait("Category", "CI")]
    public Task GuardBeforeMatch_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<TextContentGuardAnalyzer>($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{TestStubs.AngleSharpDom}}
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
        return AnalyzerTestRunner.VerifyAsync<TextContentGuardAnalyzer>($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{TestStubs.AngleSharpDom}}
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
        return AnalyzerTestRunner.VerifyAsync<TextContentGuardAnalyzer>($$"""
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
        return AnalyzerTestRunner.VerifyAsync<TextContentGuardAnalyzer>($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{TestStubs.AngleSharpDom}}
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
        return AnalyzerTestRunner.VerifyAsync<TextContentGuardAnalyzer>($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{TestStubs.AngleSharpDom}}
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
        return AnalyzerTestRunner.VerifyAsync<TextContentGuardAnalyzer>($$"""
                             using System.Linq;
                             using System.Collections.Generic;
                             {{TestStubs.AngleSharpDom}}
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
