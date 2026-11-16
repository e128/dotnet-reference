using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ParallelForEachAsyncCapBranchCoverageTests
{
    private static Task VerifyAsync(string body)
    {
        var code = "using System.Collections.Generic;\nusing System.Threading;\nusing System.Threading.Tasks;\n\n"
                   + "class MyOptions : ParallelOptions\n{\n}\n\n"
                   + "class C\n{\n    Task M(List<int> items, CancellationToken token, ParallelOptions options)\n    {\n"
                   + body
                   + "    }\n}\n";
        return AnalyzerTestRunner.VerifyAsync<ParallelForEachAsyncCapAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheOptionsAreNamedAndUncapped()
    {
        return VerifyAsync("""
                    return {|E128187:Parallel.ForEachAsync(items, parallelOptions: new ParallelOptions(), body: (item, ct) => default)|};

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNamedOptionsAreCapped()
    {
        return VerifyAsync("""
                    return Parallel.ForEachAsync(
                        source: items,
                        parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = 4 },
                        body: (item, ct) => default);

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNamedOptionsComeFromAVariable()
    {
        return VerifyAsync("""
                    return Parallel.ForEachAsync(items, parallelOptions: options, body: (item, ct) => default);

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenOnlyOtherArgumentsAreNamed()
    {
        return VerifyAsync("""
                    return {|E128187:Parallel.ForEachAsync(source: items, body: (item, ct) => default)|};

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheTokenOverloadTakesNoOptions()
    {
        return VerifyAsync("""
                    return {|E128187:Parallel.ForEachAsync(items, token, (item, ct) => default)|};

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenADerivedOptionsTypeIsCreated()
    {
        return VerifyAsync("""
                    return Parallel.ForEachAsync(items, new MyOptions(), (item, ct) => default);

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheDefaultExpressionOrLiteralIsPassed()
    {
        return VerifyAsync("""
                    _ = {|E128187:Parallel.ForEachAsync(items, default(ParallelOptions), (item, ct) => default)|};
                    return {|E128187:Parallel.ForEachAsync(items, parallelOptions: default, body: (item, ct) => default)|};

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenForEachAsyncIsCalledWithoutAReceiver()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForEachAsyncCapAnalyzer>("""
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using static System.Threading.Tasks.Parallel;

            class C
            {
                Task M(List<int> items)
                {
                    return ForEachAsync(items, (item, ct) => default);
                }
            }
            """);
    }
}
