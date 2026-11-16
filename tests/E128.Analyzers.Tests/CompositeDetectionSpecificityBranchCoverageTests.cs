using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CompositeDetectionSpecificityBranchCoverageTests
{
    private static Task VerifyAsync(string statement)
    {
        return AnalyzerTestRunner.VerifyAsync<CompositeDetectionSpecificityAnalyzer>($$"""
            abstract record DetectionRule;
            sealed record ResourceDetection(string Selector) : DetectionRule;
            sealed record DomainDetection(string Domain) : DetectionRule;
            sealed record CompositeDetection(DetectionRule[] Branches) : DetectionRule;
            class C
            {
                void M(string selector, DetectionRule[] existing)
                {
                    {{statement}}
                }
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("#A")]
    [InlineData("#item42")]
    [InlineData("#item-name")]
    [InlineData("#item_name")]
    public Task Reports_WhenASingleSelectorContainsValidIdCharacters(string selector)
    {
        return VerifyAsync($$"""var rule = {|E128079:new CompositeDetection([new ResourceDetection("{{selector}}")])|};""");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("content")]
    [InlineData(".content")]
    [InlineData("#content .item")]
    [InlineData("#content:hover")]
    public Task Silent_WhenTheSelectorIsNotASingleGenericId(string selector)
    {
        return VerifyAsync($$"""var rule = new CompositeDetection([new ResourceDetection("{{selector}}")]);""");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCompositeUsesTargetTypedCreation()
    {
        return VerifyAsync("CompositeDetection rule = {|E128079:new([new ResourceDetection(\"#content\")])|};");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("var rule = new CompositeDetection([]);")]
    [InlineData("var rule = new CompositeDetection([..existing]);")]
    [InlineData("var rule = new CompositeDetection(existing);")]
    [InlineData("var rule = new CompositeDetection([new DomainDetection(\"example.test\")]);")]
    [InlineData("var rule = new CompositeDetection([new ResourceDetection(selector)]);")]
    public Task Silent_WhenTheBranchIsNotASingleLiteralResourceSelector(string statement)
    {
        return VerifyAsync(statement);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTargetTypedCreationNamesAnotherType()
    {
        return VerifyAsync("ResourceDetection rule = new(\"#content\");");
    }
}
