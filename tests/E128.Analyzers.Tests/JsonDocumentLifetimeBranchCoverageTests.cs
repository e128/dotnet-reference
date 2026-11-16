using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers how the JsonDocument rule finds the parse call and follows the root element.
/// </summary>
public sealed class JsonDocumentLifetimeBranchCoverageTests
{
    private static Task VerifyAsync(string source)
    {
        return AnalyzerTestRunner.VerifyAsync<JsonDocumentLifetimeAnalyzer>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenParseIsCalledThroughAStaticImport()
    {
        return VerifyAsync("""
                           using System.Text.Json;
                           using static System.Text.Json.JsonDocument;
                           class C
                           {
                               void M()
                               {
                                   var doc = {|E128041:Parse("{}")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenParseIsAssignedToAnExistingLocal()
    {
        return VerifyAsync("""
                           using System.Text.Json;
                           class C
                           {
                               void M()
                               {
                                   JsonDocument doc;
                                   doc = {|E128041:JsonDocument.Parse("{}")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenParseIsTheResourceExpressionOfAUsingStatement()
    {
        return VerifyAsync("""
                           using System.Text.Json;
                           class C
                           {
                               void M()
                               {
                                   using ({|E128041:JsonDocument.Parse("{}")|})
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheUsingStatementReturnsTheRootElement()
    {
        return VerifyAsync("""
                           using System.Text.Json;
                           class C
                           {
                               JsonElement M()
                               {
                                   using (var doc = {|E128041:JsonDocument.Parse("{}")|})
                                   {
                                       return doc.RootElement;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAPropertyOfTheRootElementIsReturned()
    {
        return VerifyAsync("""
                           using System.Text.Json;
                           class C
                           {
                               JsonValueKind M()
                               {
                                   using var doc = {|E128041:JsonDocument.Parse("{}")|};
                                   return doc.RootElement.ValueKind;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheClonedChildOfTheRootElementIsReturned()
    {
        return VerifyAsync("""
                           using System.Text.Json;
                           class C
                           {
                               JsonElement M()
                               {
                                   using var doc = JsonDocument.Parse("{}");
                                   return doc.RootElement.GetProperty("a").Clone();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRootElementIsPassedAsAnArgument()
    {
        return VerifyAsync("""
                           using System.Text.Json;
                           class C
                           {
                               static string Describe(JsonElement element) => element.GetRawText();

                               string M()
                               {
                                   using var doc = JsonDocument.Parse("{}");
                                   return Describe(doc.RootElement);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRootElementIsConsumedByACall()
    {
        return VerifyAsync("""
                           using System.Text.Json;
                           class C
                           {
                               string M()
                               {
                                   using var doc = JsonDocument.Parse("{}");
                                   return doc.RootElement.GetRawText();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnEarlyReturnHasNoValue()
    {
        return VerifyAsync("""
                           using System.Text.Json;
                           class C
                           {
                               void M(bool skip)
                               {
                                   using var doc = JsonDocument.Parse("{}");
                                   if (skip)
                                   {
                                       return;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheUsingDeclarationSitsInAFieldInitializerLambda()
    {
        return VerifyAsync("""
                           using System;
                           using System.Text.Json;
                           class C
                           {
                               private static readonly Func<string, JsonElement> Reader = text =>
                               {
                                   using var doc = JsonDocument.Parse(text);
                                   return doc.RootElement;
                               };
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenParseBelongsToAnotherType()
    {
        return VerifyAsync("""
                           using System.Text.Json.Nodes;
                           class C
                           {
                               void M()
                               {
                                   var node = JsonNode.Parse("{}");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInvokedExpressionIsNotAName()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(Func<string, int>[] parsers)
                               {
                                   var value = parsers[0]("1");
                               }
                           }
                           """);
    }
}
