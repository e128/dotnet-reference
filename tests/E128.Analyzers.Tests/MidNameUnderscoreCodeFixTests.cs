using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MidNameUnderscoreCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<MidNameUnderscoreAnalyzer, MidNameUnderscoreCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_RenamesMidNameUnderscore()
    {
        const string source = """
                              class C
                              {
                                  private static int {|E128063:Nots_supportedExtensions|};
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     private static int NotsSupportedExtensions;
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_RenamesTypeNameMangle()
    {
        const string source = """
                              class C
                              {
                                  private static object {|E128063:Creates_enrichmentJsonOptions|};
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     private static object CreatesEnrichmentJsonOptions;
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_RenamesSpectresPattern()
    {
        const string source = """
                              class C
                              {
                                  private static string {|E128063:Spectres_terminal|};
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     private static string SpectresTerminal;
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticMethod_RenamesMidNameUnderscore()
    {
        const string source = """
                              class C
                              {
                                  private static void {|E128063:Process_batch|}() { }
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     private static void ProcessBatch() { }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HungarianPrefixWithMidNameUnderscore_PreservesPrefix()
    {
        const string source = """
                              class C
                              {
                                  private static int {|E128063:s_batch_count|};
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     private static int s_batchCount;
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
