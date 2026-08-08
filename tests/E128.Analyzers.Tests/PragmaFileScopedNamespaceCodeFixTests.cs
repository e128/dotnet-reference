using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PragmaFileScopedNamespaceCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<PragmaFileScopedNamespaceAnalyzer, PragmaFileScopedNamespaceCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            // Suppress the framework's suppression-check for the same reason as the analyzer
            // tests — E128094 fires on a pragma above the namespace, and the framework adds
            // its own pragma during the suppression phase, which is also above the namespace.
            TestBehaviors = TestBehaviors.SkipSuppressionCheck
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_MovesPragma_BelowNamespaceDeclaration()
    {
        return VerifyFixAsync(
            """
            {|E128094:#pragma warning disable CA1000|}

            namespace N;

            class C
            {
            }
            """,
            """
            namespace N;
            #pragma warning disable CA1000

            class C
            {
            }
            """);
    }
}
