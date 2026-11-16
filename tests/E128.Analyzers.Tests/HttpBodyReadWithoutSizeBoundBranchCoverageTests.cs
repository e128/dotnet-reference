using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpBodyReadWithoutSizeBoundBranchCoverageTests
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
                HttpResponseMessage field = new HttpResponseMessage();

            {{members}}
            }
            """;

        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>(source);
    }

    private static string Method(string name, string body)
    {
        return $$"""
                async Task {{name}}(HttpResponseMessage response, bool flag, int count)
                {
            {{body}}
                }
            """;
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAGuardThatStopsTheFlowHoldsForANullLength()
    {
        var methods = string.Join(
            "\n\n",
            Method("OrNull", $"        if ({Length} == null || {Length} > 100) {{ throw new InvalidOperationException(); }}\n        {Read}"),
            Method("NotLessOrEqual", $"        if (!({Length} <= 100)) {{ return; }}\n        {Read}"),
            Method("NotAnd", $"        if (!({Length} != null && {Length} < 100)) {{ return; }}\n        {Read}"),
            Method("IsNull", $"        if ({Length} is null) {{ return; }}\n        {Read}"),
            Method("IsNullOr", $"        if ({Length} is null or > 100) {{ return; }}\n        {Read}"),
            Method("IsNotRange", $"        if ({Length} is not (> 0 and < 100)) {{ return; }}\n        {Read}"),
            Method("NullOnTheLeft", $"        foreach (var item in new[] {{ 1 }})\n        {{\n            if (null == {Length}) {{ continue; }}\n            {Read}\n        }}"),
            Method("BothNull", $"        if (({Length} == null) && ({Length} == null)) {{ return; }}\n        {Read}"),
            Method("InsideBlock", $"        {{\n            if ({Length} is null or > 100) {{ return; }}\n        }}\n        {Read}"),
            Method("ReadInThen", $"        if ({Length} is not null && {Length} < 100)\n        {{\n            {Read}\n        }}"),
            Method("ReadInThenRelational", $"        if ({Length} is > 0 and < 100)\n        {{\n            {Read}\n        }}"));

        return VerifyAsync(methods);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardCannotProveTheLengthWasKnown()
    {
        var methods = string.Join(
            "\n\n",
            Method("Greater", $"        if ({Length} > 100) {{ return; }}\n        {ReportedRead}"),
            Method("OrUnknown", $"        if ({Length} > 100 || flag) {{ return; }}\n        {ReportedRead}"),
            Method("AndUnknown", $"        if ({Length} == null && flag) {{ return; }}\n        {ReportedRead}"),
            Method("NotOrUnknown", $"        if (!(flag || {Length} > 1)) {{ return; }}\n        {ReportedRead}"),
            Method("DeclarationPattern", $"        if ({Length} is long known) {{ return; }}\n        {ReportedRead}"),
            Method("ConstantPattern", $"        if ({Length} is 5) {{ return; }}\n        {ReportedRead}"),
            Method("NotNull", $"        if ({Length} != null) {{ return; }}\n        {ReportedRead}"),
            Method("ReadWhenNull", $"        if ({Length} == null)\n        {{\n            {ReportedRead}\n        }}"),
            Method("NoTermination", $"        if ({Length} == null) {{ count++; }}\n        {ReportedRead}"),
            Method("NegatedUnknown", $"        if (!({Length}.HasValue && flag)) {{ return; }}\n        {ReportedRead}"));

        return VerifyAsync(methods);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardSitsInABranchTheReadCanSkip()
    {
        var guard = $"if ({Length} is null or > 100) {{{{ throw new InvalidOperationException(); }}}}";
        var methods = string.Join(
            "\n\n",
            Method("InIf", $"        if (flag) {{ {guard} }}\n        {ReportedRead}"),
            Method("InElse", $"        if (flag) {{ count++; }} else {{ {guard} }}\n        {ReportedRead}"),
            Method("InCatch", $"        try {{ count++; }} catch (InvalidOperationException) {{ {guard} }}\n        {ReportedRead}"),
            Method("InFinally", $"        try {{ count++; }} finally {{ {guard} }}\n        {ReportedRead}"),
            Method("InFor", $"        for (var i = 0; i < 1; i++) {{ {guard} }}\n        {ReportedRead}"),
            Method("InForEach", $"        foreach (var i in new[] {{ 1 }}) {{ {guard} }}\n        {ReportedRead}"),
            Method("InWhile", $"        while (flag) {{ {guard} }}\n        {ReportedRead}"),
            Method("InDo", $"        do {{ {guard} }} while (flag);\n        {ReportedRead}"),
            Method("InUnbracedWhile", $"        while (flag) {{ {guard} }}\n        {ReportedRead}"),
            Method("InLambda", $"        Action inner = () => {{ {guard} }};\n        {ReportedRead}"),
            Method("InLocalFunction", $"        void Inner() {{ {guard} }}\n        {ReportedRead}"),
            Method("AfterTheRead", $"        {ReportedRead}\n        {guard}"));

        return VerifyAsync(methods);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLengthLivesInALocalOfTheSameChain()
    {
        var methods = string.Join(
            "\n\n",
            Method("Plain", $"        var length = {Length};\n        if (length is null or > 100) {{ return; }}\n        {Read}"),
            Method("Parenthesized", $"        var length = ({Length});\n        if (length is null or > 100) {{ return; }}\n        {Read}"));
        var reports = string.Join(
            "\n\n",
            Method("OtherChain", $"        var other = field.Content.Headers.ContentLength;\n        if (other is null or > 100) {{ return; }}\n        {ReportedRead}"),
            Method("NotALength", $"        var small = count;\n        if (small > 100) {{ return; }}\n        {ReportedRead}"),
            Method("NotAMember", $"        var text = response.ToString();\n        if (text is null) {{ return; }}\n        {ReportedRead}"));

        return VerifyAsync(methods + "\n\n" + reports);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Resolves_TheContentChainThroughThisPostfixParenthesesAndConditionalAccess()
    {
        var silent = string.Join(
            "\n\n",
            """
                async Task ThisRoot()
                {
                    if (this.field.Content.Headers.ContentLength is null or > 100) { return; }
                    _ = await this.field.Content.ReadAsStringAsync();
                }
            """,
            """
                async Task ParenthesizedRoot(HttpResponseMessage response)
                {
                    if ((response).Content.Headers.ContentLength is null or > 100) { return; }
                    _ = await (response).Content.ReadAsStringAsync();
                }
            """,
            """
                async Task PostfixRoot(HttpResponseMessage response)
                {
                    if (response!.Content.Headers.ContentLength is null or > 100) { return; }
                    _ = await response!.Content.ReadAsStringAsync();
                }
            """,
            """
                async Task ConditionalInParentheses(HttpResponseMessage response)
                {
                    if ((response?.Content).Headers.ContentLength is null or > 100) { return; }
                    _ = await (response?.Content).ReadAsStringAsync();
                }
            """);
        var reports = string.Join(
            "\n\n",
            """
                async Task ThisAgainstPlain()
                {
                    if (this.field.Content.Headers.ContentLength is null or > 100) { return; }
                    _ = await {|E128227:field.Content.ReadAsStringAsync()|};
                }
            """,
            """
                HttpResponseMessage Get() => field;

                async Task CallRoot()
                {
                    if (Get().Content.Headers.ContentLength is null or > 100) { return; }
                    _ = await {|E128227:Get().Content.ReadAsStringAsync()|};
                }
            """);

        return VerifyAsync(silent + "\n\n" + reports);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ReadsThatLiveOutsideAMethodBodyOrInALambda()
    {
        return VerifyAsync("""
                Task<string> expressionBodied(HttpContent content) => {|E128227:content.ReadAsStringAsync()|};

                Task<string> fieldRead = {|E128227:new StreamContent(System.IO.Stream.Null).ReadAsStringAsync()|};

                async Task LambdaBodies(HttpResponseMessage response)
                {
                    Func<Task<string>> a = () => {|E128227:response.Content.ReadAsStringAsync()|};
                    Func<Task<string>> b = async () =>
                    {
                        if (response.Content.Headers.ContentLength is null or > 100) { throw new InvalidOperationException(); }
                        return await response.Content.ReadAsStringAsync();
                    };
                    await a();
                    await b();
                }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Handles_CappedBufferLoadsByRootDominanceAndOverload()
    {
        var methods = string.Join(
            "\n\n",
            Method("IntCap", $"        await response.Content.LoadIntoBufferAsync(100);\n        {Read}"),
            Method("TokenCap", $"        await response.Content.LoadIntoBufferAsync(100, CancellationToken.None);\n        {Read}"),
            Method("OtherRoot", $"        await field.Content.LoadIntoBufferAsync(100);\n        {ReportedRead}"),
            Method("NoCap", $"        await response.Content.LoadIntoBufferAsync();\n        {ReportedRead}"),
            Method("InBranch", $"        if (flag) {{ await response.Content.LoadIntoBufferAsync(100); }}\n        {ReportedRead}"),
            Method("TokenOnly", $"        await response.Content.LoadIntoBufferAsync(CancellationToken.None);\n        {ReportedRead}"),
            Method("AfterTheRead", $"        {ReportedRead}\n        await response.Content.LoadIntoBufferAsync(100);"));

        return VerifyAsync(methods);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheContentIsKnownToBeInMemoryOrNotHttpContent()
    {
        return VerifyAsync("""
                async Task InMemory(byte[] bytes)
                {
                    _ = await new ByteArrayContent(bytes).ReadAsStringAsync();
                    _ = await new ReadOnlyMemoryContent(bytes).ReadAsByteArrayAsync();
                    _ = await new StringContent("text").ReadAsStringAsync();
                    _ = await System.Net.Http.Json.JsonContent.Create(1).ReadAsStringAsync();
                }

                async Task Other(Own own)
                {
                    _ = await own.ReadAsStringAsync();
                    _ = await own.Other();
                }
            }

            class Own
            {
                public Task<string> ReadAsStringAsync() => Task.FromResult("");

                public Task<string> Other() => Task.FromResult("");
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_AContentChainThatIsNotANamedReceiver()
    {
        return VerifyAsync("""
                async Task Chains(HttpClient client)
                {
                    _ = await {|E128227:(await client.GetAsync("x")).Content.ReadAsStringAsync()|};
                    _ = await client.GetAsync("y");
                }
            """);
    }
}
