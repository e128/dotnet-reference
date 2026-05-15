using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GetAttributeElementTypeAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<GetAttributeElementTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private static DiagnosticResult Expect(int line, int column)
    {
        return new DiagnosticResult("E128078", DiagnosticSeverity.Warning)
            .WithLocation(line, column);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReportsDiagnostic_WhenHrefOnNonAnchor()
    {
        const string code = """
                            namespace AngleSharp.Dom
                            {
                                public interface IElement
                                {
                                    string GetAttribute(string name);
                                    IElement QuerySelector(string selector);
                                }
                            }

                            class Foo
                            {
                                void Parse(AngleSharp.Dom.IElement doc)
                                {
                                    var span = doc.QuerySelector("span.link");
                                    var href = span?.GetAttribute("href");
                                }
                            }
                            """;
        return VerifyAsync(code, Expect(15, 25));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoDiagnostic_WhenHrefOnAnchor()
    {
        return VerifyAsync("""
                           namespace AngleSharp.Dom
                           {
                               public interface IElement
                               {
                                   string GetAttribute(string name);
                                   IElement QuerySelector(string selector);
                               }
                           }

                           class Foo
                           {
                               void Parse(AngleSharp.Dom.IElement doc)
                               {
                                   var anchor = doc.QuerySelector("a.nav-link");
                                   var href = anchor?.GetAttribute("href");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoDiagnostic_WhenHrefOnLink()
    {
        return VerifyAsync("""
                           namespace AngleSharp.Dom
                           {
                               public interface IElement
                               {
                                   string GetAttribute(string name);
                                   IElement QuerySelector(string selector);
                               }
                           }

                           class Foo
                           {
                               void Parse(AngleSharp.Dom.IElement doc)
                               {
                                   var canonical = doc.QuerySelector("link[rel='canonical']");
                                   var href = canonical?.GetAttribute("href");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoDiagnostic_WhenNonHrefAttribute()
    {
        return VerifyAsync("""
                           namespace AngleSharp.Dom
                           {
                               public interface IElement
                               {
                                   string GetAttribute(string name);
                                   IElement QuerySelector(string selector);
                               }
                           }

                           class Foo
                           {
                               void Parse(AngleSharp.Dom.IElement doc)
                               {
                                   var span = doc.QuerySelector("span.info");
                                   var cls = span?.GetAttribute("class");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Initializes_WithoutTypeLoadException()
    {
        var analyzer = new GetAttributeElementTypeAnalyzer();
        _ = Assert.Single(analyzer.SupportedDiagnostics);
    }
}
