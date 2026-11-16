using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CompositeDetectionSpecificityAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string statement)
    {
        return AnalyzerTestRunner.VerifyAsync<CompositeDetectionSpecificityAnalyzer>(
            $$"""
            namespace Detections
            {
                public abstract record DetectionRule;
                public sealed record ResourceDetection(string Selector, string Extra = "") : DetectionRule;
                public sealed record CompositeDetection(DetectionRule[] Branches, string Extra = "") : DetectionRule
                {
                    public CompositeDetection() : this([]) { }
                }
                public sealed class Box<T>(T[] values)
                {
                    public T[] Values { get; } = values;
                }
            }
            class C
            {
                void M()
                {
                    {{statement}}
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenBothTypesAreNamespaceQualified()
    {
        return VerifyAsync(
            """var rule = {|E128079:new Detections.CompositeDetection([new Detections.ResourceDetection("#content")])|};""");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompositeNameIsQualifiedButDiffers()
    {
        return VerifyAsync("""var rule = new Detections.ResourceDetection("#content");""");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompositeIsAGenericType()
    {
        return VerifyAsync(
            """var rule = new Detections.Box<Detections.DetectionRule>([new Detections.ResourceDetection("#content")]);""");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInnerDetectionIsAQualifiedOtherType()
    {
        return VerifyAsync(
            """var rule = new Detections.CompositeDetection([new System.Text.StringBuilder("#content")]);""");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInnerDetectionIsAGenericType()
    {
        return VerifyAsync(
            "var rule = new Detections.CompositeDetection([new System.Collections.Generic.List<string>()]);");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompositeHasNoArgumentList()
    {
        return VerifyAsync("var rule = new Detections.CompositeDetection { };");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompositeHasNoArguments()
    {
        return VerifyAsync("var rule = new Detections.CompositeDetection();");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompositeHasTwoArguments()
    {
        return VerifyAsync(
            """var rule = new Detections.CompositeDetection([new Detections.ResourceDetection("#content")], "x");""");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResourceDetectionHasNoArgumentList()
    {
        return VerifyAsync("var rule = new Detections.CompositeDetection([new Detections.ResourceDetection { }]);");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResourceDetectionHasTwoArguments()
    {
        return VerifyAsync(
            """var rule = new Detections.CompositeDetection([new Detections.ResourceDetection("#content", "x")]);""");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("#item{")]
    [InlineData("#item~")]
    [InlineData("#café")]
    [InlineData("#item[")]
    [InlineData("#item^")]
    [InlineData("#item`")]
    public Task Silent_WhenTheSelectorHoldsACharacterAboveTheLetterRange(string selector)
    {
        return VerifyAsync($$"""var rule = new Detections.CompositeDetection([new Detections.ResourceDetection("{{selector}}")]);""");
    }
}
