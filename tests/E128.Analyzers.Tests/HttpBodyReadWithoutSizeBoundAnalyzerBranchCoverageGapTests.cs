using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpBodyReadWithoutSizeBoundAnalyzerBranchCoverageGapTests
{
    private const string Length = "response.Content.Headers.ContentLength";

    private const string Read = "_ = await response.Content.ReadAsStringAsync();";

    private const string ReportedRead = "_ = await {|E128227:response.Content.ReadAsStringAsync()|};";

    private static Task VerifyAsync(string members)
    {
        var source = $$"""
            using System;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            class C
            {
            {{members}}
            }
            """;

        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>(source);
    }

    private static string Method(string name, string body)
    {
        return $$"""
                async Task {{name}}(HttpResponseMessage response, bool flag, int value)
                {
            {{body}}
                }
            """;
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardSitsInABlockInsideASwitchSection()
    {
        var guard = $"if ({Length} is null or > 100) {{ throw new InvalidOperationException(); }}";

        return VerifyAsync(Method(
            "InSwitchSection",
            $"        switch (value)\n        {{\n            case 1:\n                value++;\n                {{ {guard} }}\n                break;\n            default:\n                break;\n        }}\n        {ReportedRead}"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardAndTheReadShareASwitchSection()
    {
        var guard = $"if ({Length} is null or > 100) {{ throw new InvalidOperationException(); }}";

        return VerifyAsync(Method(
            "SameSection",
            $"        switch (value)\n        {{\n            case 1:\n                {guard}\n                {Read}\n                break;\n        }}"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Handles_GuardsWhoseOperandsAreUnknownWhenTheLengthIsNull()
    {
        var methods = string.Join(
            "\n\n",
            Method("BothRelational", $"        if ({Length} > 1 || {Length} < 5) {{ return; }}\n        {ReportedRead}"),
            Method("UnknownThenNull", $"        if (flag || {Length} == null) {{ return; }}\n        {Read}"),
            Method("UnknownBoth", $"        if (flag || !flag) {{ return; }}\n        {ReportedRead}"),
            Method("RelationalOrUnknown", $"        if ({Length} > 1 || flag) {{ return; }}\n        {ReportedRead}"),
            Method("ReadInThenWhenUnknown", $"        if (flag || {Length} > 100)\n        {{\n            {ReportedRead}\n        }}"),
            Method("UnknownAndNull", $"        if (flag && {Length} == null) {{ return; }}\n        {ReportedRead}"));

        return VerifyAsync(methods);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Handles_ThenBranchesThatContainNestedFunctionsOrBlocks()
    {
        var methods = string.Join(
            "\n\n",
            Method("LambdaOnly", $"        if ({Length} is null or > 100) {{ Action leave = () => {{ return; }}; leave(); }}\n        {ReportedRead}"),
            Method("LocalFunctionOnly", $"        if ({Length} is null or > 100) {{ void Leave() {{ return; }} Leave(); }}\n        {ReportedRead}"),
            Method("NestedBlock", $"        if ({Length} is null or > 100) {{ {{ return; }} }}\n        {Read}"));

        return VerifyAsync(methods);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Handles_LocalsWithoutAnInitializerAndConditionsOnOtherMembers()
    {
        var methods = string.Join(
            "\n\n",
            Method("Uninitialized", $"        int unset;\n        unset = value;\n        if ({Length} is null or > 100) {{ return; }}\n        {Read}"),
            Method("MethodGroupInCondition", $"        if (response.ToString() is null) {{ return; }}\n        {ReportedRead}"));

        return VerifyAsync(methods);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheReceiverIsAPostfixIncrement()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>(
            """
            using System.IO;
            using System.Net;
            using System.Net.Http;
            using System.Threading.Tasks;

            sealed class Counted : HttpContent
            {
                public static Counted operator ++(Counted content) => content;

                protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

                protected override bool TryComputeLength(out long length)
                {
                    length = 0;
                    return false;
                }
            }

            class C
            {
                async Task M(Counted content)
                {
                    _ = await {|E128227:(content++).ReadAsStringAsync()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReadCallDoesNotBind()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>(
            """
            using System.Threading.Tasks;

            class C
            {
                async Task M()
                {
                    _ = await missing.ReadAsStringAsync();
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompilationHasNoHttpContent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>(
            """
            class Own
            {
                public int ReadAsStringAsync() => 0;
            }

            class C
            {
                int M(Own own) => own.ReadAsStringAsync();
            }
            """,
            options: new AnalyzerTestOptions { References = ReferenceAssemblies.NetStandard.NetStandard10 });
    }
}
