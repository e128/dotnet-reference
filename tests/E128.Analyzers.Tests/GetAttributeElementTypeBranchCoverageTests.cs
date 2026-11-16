using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers how the href rule traces a receiver back to the selector that created it.
/// </summary>
public sealed class GetAttributeElementTypeBranchCoverageTests
{
    private const string Stubs = """
                                 namespace AngleSharp.Dom
                                 {
                                     public interface IElement
                                     {
                                         string GetAttribute(string name);
                                         string GetAttribute(string name, string fallback);
                                         IElement QuerySelector(string selector);
                                         IElement QuerySelector(string selector, string extra);
                                         IElement QuerySelectorAll(string selector);
                                         IElement Closest(string selector);
                                         IElement Child { get; }
                                     }
                                 }

                                 """;

    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<GetAttributeElementTypeAnalyzer>(
            Stubs + """
                    class Foo
                    {
                        void Parse(AngleSharp.Dom.IElement doc, string dynamicSelector)
                        {
                    """ + "\n" + body + "\n    }\n}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallUsesPlainMemberAccess()
    {
        return VerifyAsync("""
                                   var span = doc.QuerySelector("span");
                                   var href = {|E128078:span.GetAttribute("href")|};
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSelectorComesFromQuerySelectorAll()
    {
        return VerifyAsync("""
                                   var items = doc.QuerySelectorAll("div.item");
                                   var href = {|E128078:items.GetAttribute("href")|};
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLocalIsInitializedThroughAConditionalAccess()
    {
        return VerifyAsync("""
                                   var span = doc?.QuerySelector("h1");
                                   var href = span?{|E128078:.GetAttribute("href")|};
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAHyphenatedAndDigitElementName()
    {
        return VerifyAsync("""
                                   var custom = doc.QuerySelector("custom-el2[data-x]");
                                   var href = {|E128078:custom.GetAttribute("href")|};
                           """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("span:hover")]
    [InlineData("span~p")]
    [InlineData("span_x")]
    [InlineData("span.x")]
    [InlineData("span{x")]
    public Task Reports_AndStopsTheElementNameAtTheFirstSelectorSymbol(string selector)
    {
        return VerifyAsync($$"""
                                     var node = doc.QuerySelector("{{selector}}");
                                     var href = {|E128078:node.GetAttribute("href")|};
                             """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("A")]
    [InlineData("BASE")]
    [InlineData("Area")]
    public Task Silent_ForHrefElementNamesInAnyCase(string element)
    {
        return VerifyAsync($$"""
                                     var node = doc.QuerySelector("{{element}}");
                                     var href = node.GetAttribute("href");
                             """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("")]
    [InlineData(".link")]
    [InlineData("#main")]
    [InlineData("[data-x]")]
    public Task Silent_WhenTheSelectorNamesNoElement(string selector)
    {
        return VerifyAsync($$"""
                                     var node = doc.QuerySelector("{{selector}}");
                                     var href = node.GetAttribute("href");
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsAChainedCall()
    {
        return VerifyAsync("""
                                   var href = doc.QuerySelector("span").GetAttribute("href");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsAParameter()
    {
        return VerifyAsync("""
                                   var href = doc.GetAttribute("href");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalHasNoInitializer()
    {
        return VerifyAsync("""
                                   AngleSharp.Dom.IElement node;
                                   node = doc.QuerySelector("span");
                                   var href = node.GetAttribute("href");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsInitializedFromAnotherLocal()
    {
        return VerifyAsync("""
                                   var first = doc.QuerySelector("span");
                                   var second = first;
                                   var href = second.GetAttribute("href");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalComesFromAForeachVariable()
    {
        return VerifyAsync("""
                                   foreach (var node in new[] { doc })
                                   {
                                       var href = node.GetAttribute("href");
                                   }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInitializerIsAnUnqualifiedCall()
    {
        return VerifyAsync("""
                                   AngleSharp.Dom.IElement Find(string selector) => doc;
                                   var node = Find("span");
                                   var href = node.GetAttribute("href");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInitializerIsAnotherMethod()
    {
        return VerifyAsync("""
                                   var node = doc.Closest("span");
                                   var href = node.GetAttribute("href");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSelectorIsNotALiteral()
    {
        return VerifyAsync("""
                                   var node = doc.QuerySelector(dynamicSelector);
                                   var href = node.GetAttribute("href");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInitializerIsAPropertyRead()
    {
        return VerifyAsync("""
                                   var node = doc.Child;
                                   var href = node.GetAttribute("href");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConditionalReceiverIsNotAnIdentifier()
    {
        return VerifyAsync("""
                                   var href = doc?.Child?.GetAttribute("href");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenGetAttributeHasTwoArguments()
    {
        return VerifyAsync("""
                                   var span = doc.QuerySelector("span");
                                   var href = span.GetAttribute("href", "none");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAttributeNameIsNotALiteral()
    {
        return VerifyAsync("""
                                   var span = doc.QuerySelector("span");
                                   var href = span.GetAttribute(dynamicSelector);
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsNotAMemberCall()
    {
        return VerifyAsync("""
                                   string GetAttribute(string name) => name;
                                   var href = GetAttribute("href");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMethodIsNotGetAttribute()
    {
        return VerifyAsync("""
                                   var span = doc.QuerySelector("span");
                                   var other = span.ToString();
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSelectorQueryHasTwoArguments()
    {
        return VerifyAsync("""
                                   var node = doc.QuerySelector("span", "extra");
                                   var href = node.GetAttribute("href");
                           """);
    }
}
