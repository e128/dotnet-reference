using System.Threading.Tasks;
using E128.Analyzers.Style;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GuidNewGuidTempPathE128AnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInterpolationFeedsAnObjectCreation()
    {
        return AnalyzerTestRunner.VerifyAsync<GuidNewGuidTempPathE128Analyzer>("""
            using System;
            using System.IO;
            class C
            {
                FileInfo M() => new FileInfo($"{Guid.NewGuid()}.tmp");
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInterpolationFeedsAnUnqualifiedCall()
    {
        return AnalyzerTestRunner.VerifyAsync<GuidNewGuidTempPathE128Analyzer>("""
            using System;
            class C
            {
                static string Name(string value) => value;

                string M() => Name($"{Guid.NewGuid()}.tmp");
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInterpolationFeedsAnotherPathMethodOrReceiver()
    {
        return AnalyzerTestRunner.VerifyAsync<GuidNewGuidTempPathE128Analyzer>("""
            using System;
            using System.IO;
            class Paths
            {
                public static string Combine(string value) => value;
            }
            class C
            {
                string M(string root) => Path.Join(root, $"{Guid.NewGuid()}.tmp");

                string N() => Paths.Combine($"{Guid.NewGuid()}.tmp");
            }
            """);
    }
}
