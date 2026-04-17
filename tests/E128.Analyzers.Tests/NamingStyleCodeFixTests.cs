using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NamingStyleCodeFixTests
{
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("myFieldName", new[] { "my", "Field", "Name" })]
    [InlineData("MyFieldName", new[] { "My", "Field", "Name" })]
    [InlineData("HTTPClient", new[] { "HTTP", "Client" })]
    [InlineData("foo", new[] { "foo" })]
    public void SplitByCaseTransition_ReturnsExpectedWords(string input, string[] expected)
    {
        var result = NamingStyleCodeFixProvider.SplitByCaseTransition(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [Trait("Category", "CI")]
    // prefix "_", CamelCase
    [InlineData("myField", "_", "", "", "CamelCase", "_myField")] // already camelCase
    [InlineData("MyField", "_", "", "", "CamelCase", "_myField")] // PascalCase → lowercase first char
    [InlineData("_MyField", "_", "", "", "CamelCase", "_myField")] // wrong case after prefix → replace
    // PascalCase, no prefix
    [InlineData("myProp", "", "", "", "PascalCase", "MyProp")]
    [InlineData("maxRetry", "", "", "", "PascalCase", "MaxRetry")]
    [InlineData("myService", "", "", "", "PascalCase", "MyService")]
    [InlineData("getResult", "", "", "", "PascalCase", "GetResult")]
    // interface prefix "I"
    [InlineData("Fetcher", "I", "", "", "PascalCase", "IFetcher")]
    [InlineData("iFetcher", "I", "", "", "PascalCase", "IFetcher")] // wrong-case prefix, no double "II"
    // AllUpperCase with separator
    [InlineData("maxRetryCount", "", "", "_", "AllUpperCase", "MAX_RETRY_COUNT")]
    // edge cases: "I"/"T" prefix guard — letter after prefix must be uppercase
    [InlineData("IndexFilenames", "", "", "", "PascalCase", "IndexFilenames")] // "I" is word-initial, not prefix
    [InlineData("ToolbarSelectors", "", "", "", "PascalCase", "ToolbarSelectors")] // "T" is word-initial, not prefix
    [InlineData("IList", "", "", "", "PascalCase", "List")] // "I" IS real interface prefix
    public void BuildCompliantName_ReturnsExpectedName(
        string symbolName,
        string prefix,
        string suffix,
        string wordSeparator,
        string capitalizationScheme,
        string expected)
    {
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName, prefix, suffix, wordSeparator, capitalizationScheme);
        Assert.Equal(expected, result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ComputeCompliantName_SuggestedNameProperty_ReturnsPrecomputed()
    {
        // When Roslyn's IDE1006 embeds "SuggestedName", the code fix should use it directly
        // instead of recomputing from SymbolName/Prefix/Suffix/CapitalizationScheme.
        var properties = ImmutableDictionary.CreateRange(
            [
                new KeyValuePair<string, string?>("SuggestedName", "_myField"),
                new KeyValuePair<string, string?>("SymbolName", "myField"),
                new KeyValuePair<string, string?>("Prefix", "_"),
                new KeyValuePair<string, string?>("CapitalizationScheme", "CamelCase")
            ]);

        var diagnostic = Diagnostic.Create(
            new DiagnosticDescriptor("IDE1006", "Test", "Test", "Style", DiagnosticSeverity.Warning, true),
            Location.None, properties);

        var result = NamingStyleCodeFixProvider.ComputeCompliantName(diagnostic);
        Assert.Equal("_myField", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ComputeCompliantName_NoSuggestedName_ComputesFromProperties()
    {
        // When "SuggestedName" is absent (e.g., from FakeNamingViolationAnalyzer in tests),
        // the code fix falls back to computing from SymbolName + Prefix + CapitalizationScheme.
        var properties = ImmutableDictionary.CreateRange(
            [
                new KeyValuePair<string, string?>("SymbolName", "myField"),
                new KeyValuePair<string, string?>("Prefix", "_"),
                new KeyValuePair<string, string?>("Suffix", string.Empty),
                new KeyValuePair<string, string?>("WordSeparator", string.Empty),
                new KeyValuePair<string, string?>("CapitalizationScheme", "CamelCase")
            ]);

        var diagnostic = Diagnostic.Create(
            new DiagnosticDescriptor("IDE1006", "Test", "Test", "Style", DiagnosticSeverity.Warning, true),
            Location.None, properties);

        var result = NamingStyleCodeFixProvider.ComputeCompliantName(diagnostic);
        Assert.Equal("_myField", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PrivateField_RenamesWithUnderscorePrefix()
    {
        const string source = """
                              class C
                              {
                                  private int {|IDE1006:myField|} = 0;
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     private int _myField = 0;
                                 }
                                 """;

        return new CSharpCodeFixTest<FakeNamingViolationAnalyzer, NamingStyleCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PrivateField_PascalCase_LowercasesFirstCharAndAddsPrefix()
    {
        const string source = """
                              class C
                              {
                                  private int {|IDE1006:MyField|} = 0;
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     private int _myField = 0;
                                 }
                                 """;

        return new CSharpCodeFixTest<FakeNamingViolationAnalyzer, NamingStyleCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PrivateField_PropagatesRenameToReferences()
    {
        const string source = """
                              class C
                              {
                                  private int {|IDE1006:myField|} = 0;

                                  public int GetField() => myField;
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     private int _myField = 0;

                                     public int GetField() => _myField;
                                 }
                                 """;

        return new CSharpCodeFixTest<FakeNamingViolationAnalyzer, NamingStyleCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }
}
