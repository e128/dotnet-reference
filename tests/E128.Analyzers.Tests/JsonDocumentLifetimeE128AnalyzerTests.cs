using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class JsonDocumentLifetimeE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<JsonDocumentLifetimeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task JsonDocumentParse_NoUsingScope_Fires()
    {
        return VerifyAsync("""
            using System.Text.Json;
            class C
            {
                void M()
                {
                    var doc = {|E128041:JsonDocument.Parse("{}")|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task JsonDocumentParse_UsingDeclaration_NoDiagnostic()
    {
        return VerifyAsync("""
            using System.Text.Json;
            class C
            {
                void M()
                {
                    using var doc = JsonDocument.Parse("{}");
                    var root = doc.RootElement;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task JsonDocumentParse_UsingDeclaration_RootElementEscapes_Fires()
    {
        return VerifyAsync("""
            using System.Text.Json;
            class C
            {
                JsonElement M()
                {
                    using var doc = {|E128041:JsonDocument.Parse("{}")|};
                    return doc.RootElement;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task JsonDocumentParse_UsingDeclaration_RootElementCloned_NoDiagnostic()
    {
        return VerifyAsync("""
            using System.Text.Json;
            class C
            {
                JsonElement M()
                {
                    using var doc = JsonDocument.Parse("{}");
                    return doc.RootElement.Clone();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task JsonDocumentParse_UsingStatement_NoDiagnostic()
    {
        return VerifyAsync("""
            using System.Text.Json;
            class C
            {
                void M()
                {
                    using (var doc = JsonDocument.Parse("{}"))
                    {
                        var root = doc.RootElement;
                    }
                }
            }
            """);
    }
}
