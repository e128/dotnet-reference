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
    [Fact]
    [Trait("Category", "CI")]
    public void SplitByCaseTransition_CamelCase_SplitsCorrectly()
    {
        var result = NamingStyleCodeFixProvider.SplitByCaseTransition("myFieldName");
        Assert.Equal(["my", "Field", "Name"], result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void SplitByCaseTransition_PascalCase_SplitsCorrectly()
    {
        var result = NamingStyleCodeFixProvider.SplitByCaseTransition("MyFieldName");
        Assert.Equal(["My", "Field", "Name"], result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void SplitByCaseTransition_Acronym_KeepsAcronymTogether()
    {
        var result = NamingStyleCodeFixProvider.SplitByCaseTransition("HTTPClient");
        Assert.Equal(["HTTP", "Client"], result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void SplitByCaseTransition_SingleWord_ReturnsOneElement()
    {
        var result = NamingStyleCodeFixProvider.SplitByCaseTransition("foo");
        Assert.Equal(["foo"], result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_AddUnderscorePrefix_CamelCase()
    {
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "myField",
            prefix: "_",
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "CamelCase");

        Assert.Equal("_myField", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_AddUnderscorePrefix_LowercasesFirstChar()
    {
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "MyField",
            prefix: "_",
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "CamelCase");

        Assert.Equal("_myField", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_WrongPrefix_ReplacesWithCorrect()
    {
        // _MyField (wrong case after _) → _myField
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "_MyField",
            prefix: "_",
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "CamelCase");

        Assert.Equal("_myField", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_PascalCase_CapitalizesFirstChar()
    {
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "myProp",
            prefix: string.Empty,
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "PascalCase");

        Assert.Equal("MyProp", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_InterfacePrefix_PrependI()
    {
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "Fetcher",
            prefix: "I",
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "PascalCase");

        Assert.Equal("IFetcher", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_AllUpper_WithSeparator()
    {
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "maxRetryCount",
            prefix: string.Empty,
            suffix: string.Empty,
            wordSeparator: "_",
            capitalizationScheme: "AllUpperCase");

        Assert.Equal("MAX_RETRY_COUNT", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_ConstField_CamelCase_ProducesPascalCase()
    {
        // constant_fields_should_be_pascal_case: no prefix, PascalCase
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "maxRetry",
            prefix: string.Empty,
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "PascalCase");

        Assert.Equal("MaxRetry", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_Type_CamelCase_ProducesPascalCase()
    {
        // types_should_be_pascal_case: no prefix, PascalCase
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "myService",
            prefix: string.Empty,
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "PascalCase");

        Assert.Equal("MyService", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_NonFieldMember_CamelCase_ProducesPascalCase()
    {
        // non_field_members_should_be_pascal_case: no prefix, PascalCase (properties/events/methods)
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "getResult",
            prefix: string.Empty,
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "PascalCase");

        Assert.Equal("GetResult", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_Interface_WrongCasePrefix_DoesNotDoublePrefix()
    {
        // interface_should_be_begins_with_i: prefix "I", PascalCase.
        // "iFetcher" has the prefix in wrong case; without the fix this produced "IIFetcher".
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "iFetcher",
            prefix: "I",
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "PascalCase");

        Assert.Equal("IFetcher", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ComputeCompliantName_SuggestedNameProperty_ReturnsPrecomputed()
    {
        // When Roslyn's IDE1006 embeds "SuggestedName", the code fix should use it directly
        // instead of recomputing from SymbolName/Prefix/Suffix/CapitalizationScheme.
        var properties = ImmutableDictionary.CreateRange<string, string?>(
        [
            new("SuggestedName", "_myField"),
            new("SymbolName", "myField"),
            new("Prefix", "_"),
            new("CapitalizationScheme", "CamelCase"),
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
        var properties = ImmutableDictionary.CreateRange<string, string?>(
        [
            new("SymbolName", "myField"),
            new("Prefix", "_"),
            new("Suffix", string.Empty),
            new("WordSeparator", string.Empty),
            new("CapitalizationScheme", "CamelCase"),
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
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = 1,
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
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = 1,
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
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_PascalCase_PreservesLeadingI_WhenNotInterfacePrefix()
    {
        // "IndexFilenames" starts with "I" but it is not an interface-prefixed name —
        // "ndex..." does not begin with an uppercase letter. Previously misidentified
        // "I" as the interface prefix and produced "NdexFilenames".
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "IndexFilenames",
            prefix: string.Empty,
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "PascalCase");

        Assert.Equal("IndexFilenames", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_PascalCase_PreservesLeadingT_WhenNotTypeParamPrefix()
    {
        // "ToolbarSelectors" starts with "T" but is not a type-param-prefixed name.
        // Previously misidentified "T" as the type param prefix and produced "OolbarSelectors".
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "ToolbarSelectors",
            prefix: string.Empty,
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "PascalCase");

        Assert.Equal("ToolbarSelectors", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildCompliantName_PascalCase_StripsRealIPrefix_WhenFollowedByUppercase()
    {
        // "IList" starts with "I" followed by uppercase "L" — this IS a real interface
        // prefix; stripping it is correct when renaming to no-prefix PascalCase.
        var result = NamingStyleCodeFixProvider.BuildCompliantName(
            symbolName: "IList",
            prefix: string.Empty,
            suffix: string.Empty,
            wordSeparator: string.Empty,
            capitalizationScheme: "PascalCase");

        Assert.Equal("List", result);
    }
}
