using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class BareParseBranchCoverageTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>(
            "using System;\nusing System.IO;\n"
            + "class NoTryParse { public static int Parse(string text) => 0; }\n"
            + "class InstanceTryParse { public static int Parse(string text) => 0; public bool TryParse(string text) => true; }\n"
            + "class VoidTryParse { public static int Parse(string text) => 0; public static void TryParse(string text) { } }\n"
            + "class WithTryParse { public static int Parse(string text) => 0; public static bool TryParse(string text, out int value) { value = 0; return true; } }\n"
            + "class C\n{\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForTypesWithoutAUsableTryParse()
    {
        return VerifyAsync("""
                void M(string text)
                {
                    var a = NoTryParse.Parse(text);
                    var b = InstanceTryParse.Parse(text);
                    var c = VoidTryParse.Parse(text);
                    Local(text);

                    static void Local(string value) { }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForMembersThatAreNotNamedParse()
    {
        return VerifyAsync("""
                void M(string text)
                {
                    var a = int.TryParse(text, out var value);
                    var b = WithTryParse.TryParse(text, out value);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAUserTypeWithAStaticBoolTryParse()
    {
        return VerifyAsync("""
                void M(string text)
                {
                    var a = {|E128089:WithTryParse.Parse(text)|};
                }

            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("catch (FormatException) { }")]
    [InlineData("catch (Exception) { }")]
    [InlineData("catch { }")]
    [InlineData("catch (IOException) { } catch (FormatException) { }")]
    public Task Silent_WhenACatchGuardsTheParse(string catches)
    {
        return VerifyAsync($$"""
                void M(string text)
                {
                    try
                    {
                        var a = int.Parse(text);
                    }
                    {{catches}}
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyAnUnrelatedCatchGuardsTheParse()
    {
        return VerifyAsync("""
                void M(string text)
                {
                    try
                    {
                        var a = {|E128089:int.Parse(text)|};
                    }
                    catch (IOException)
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheParseSitsInTheCatchBlock()
    {
        return VerifyAsync("""
                void M(string text)
                {
                    try
                    {
                    }
                    catch (FormatException)
                    {
                        var a = {|E128089:int.Parse(text)|};
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnOuterTryGuardsAnInnerTryWithoutAFormatCatch()
    {
        return VerifyAsync("""
                void M(string text)
                {
                    try
                    {
                        try
                        {
                            var a = int.Parse(text);
                        }
                        catch (IOException)
                        {
                        }
                    }
                    catch (FormatException)
                    {
                    }
                }

            """);
    }
}
