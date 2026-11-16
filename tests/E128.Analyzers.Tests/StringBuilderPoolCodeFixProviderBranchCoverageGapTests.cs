using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringBuilderPoolCodeFixProviderBranchCoverageGapTests
{
    private const string Pool = """
                                namespace Pug.Core.Text
                                {
                                    public abstract class StringBuilderPool
                                    {
                                        public static StringBuilderPool Shared => null;
                                        public abstract System.Text.StringBuilder Rent();
                                        public abstract System.Text.StringBuilder Rent(int capacity);
                                        public abstract void Return(System.Text.StringBuilder builder);
                                    }
                                }
                                """;

    private static string Source(string body)
    {
        return "using System.Text;\n" + Pool + "\nclass C\n{\n    void M()\n    {\n" + body + "\n    }\n}\n";
    }

    private static string FixedSource(string body)
    {
        return "using Pug.Core.Text;\nusing System.Text;\n" + Pool + "\nclass C\n{\n    void M()\n    {\n" + body + "\n    }\n}\n";
    }

    private static Task VerifyFixAsync(string body, string fixedBody)
    {
        return CodeFixTestRunner.RunAsync<StringBuilderPoolAnalyzer, StringBuilderPoolCodeFixProvider>(
            Source(body),
            FixedSource(fixedBody));
    }

    private static Task VerifyNoFixAsync(string body)
    {
        return CodeFixTestRunner.NoFixAsync<StringBuilderPoolAnalyzer, StringBuilderPoolCodeFixProvider>(Source(body));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesATargetTypedCreation()
    {
        return VerifyFixAsync(
            "        StringBuilder sb = {|E128081:new()|};\n        _ = sb.Append('a');",
            "        StringBuilder sb = StringBuilderPool.Shared.Rent();\n        _ = sb.Append('a');");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_DropsTheCapacityOfATargetTypedCreation()
    {
        return VerifyFixAsync(
            "        StringBuilder sb = {|E128081:new(64)|};\n        _ = sb.Append('a');",
            "        StringBuilder sb = StringBuilderPool.Shared.Rent();\n        _ = sb.Append('a');");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesACreationWithoutAnArgumentList()
    {
        return VerifyFixAsync(
            "        var sb = {|E128081:new StringBuilder { Capacity = 64 }|};\n        _ = sb.Append('a');",
            "        var sb = StringBuilderPool.Shared.Rent();\n        _ = sb.Append('a');");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCreationCarriesASeedString()
    {
        return VerifyNoFixAsync(
            "        var sb = {|E128081:new StringBuilder(\"seed\")|};\n        _ = sb.Append('a');");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCreationCarriesTwoArguments()
    {
        return VerifyNoFixAsync(
            "        var sb = {|E128081:new StringBuilder(16, 1024)|};\n        _ = sb.Append('a');");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheTargetTypedCreationCarriesASeedString()
    {
        return VerifyNoFixAsync(
            "        StringBuilder sb = {|E128081:new(\"seed\")|};\n        _ = sb.Append('a');");
    }
}
