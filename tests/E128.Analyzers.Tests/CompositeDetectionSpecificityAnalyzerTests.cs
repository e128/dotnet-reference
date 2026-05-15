using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CompositeDetectionSpecificityAnalyzerTests
{
    private const string DetectionRuleStubs = """
                                              namespace Harvest.Conversion
                                              {
                                                  public abstract record DetectionRule
                                                  {
                                                      public sealed record DomainDetection(string[] Patterns) : DetectionRule;
                                                      public sealed record ResourceDetection(string UrlPattern) : DetectionRule;
                                                      public sealed record MetaTagDetection(string Name, string Pattern) : DetectionRule;
                                                      public sealed record CompositeDetection(DetectionRule[] Branches) : DetectionRule;
                                                  }
                                              }
                                              """;

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<CompositeDetectionSpecificityAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenSingleResourceDetectionWithGenericIdSelector()
    {
        return VerifyAsync($$"""
                             {{DetectionRuleStubs}}
                             class C
                             {
                                 void M()
                                 {
                                     var d = {|#0:new Harvest.Conversion.DetectionRule.CompositeDetection([
                                         new Harvest.Conversion.DetectionRule.ResourceDetection("#content")
                                     ])|};
                                 }
                             }
                             """,
            new DiagnosticResult(CompositeDetectionSpecificityAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("#content"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenMultipleBranches()
    {
        return VerifyAsync($$"""
                             {{DetectionRuleStubs}}
                             class C
                             {
                                 void M()
                                 {
                                     var d = new Harvest.Conversion.DetectionRule.CompositeDetection([
                                         new Harvest.Conversion.DetectionRule.ResourceDetection("#content"),
                                         new Harvest.Conversion.DetectionRule.ResourceDetection(".post-class")
                                     ]);
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenSingleDomainDetection()
    {
        return VerifyAsync($$"""
                             {{DetectionRuleStubs}}
                             class C
                             {
                                 void M()
                                 {
                                     var d = new Harvest.Conversion.DetectionRule.CompositeDetection([
                                         new Harvest.Conversion.DetectionRule.DomainDetection(["example.com"])
                                     ]);
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenSingleResourceDetectionWithMainId()
    {
        return VerifyAsync($$"""
                             {{DetectionRuleStubs}}
                             class C
                             {
                                 void M()
                                 {
                                     var d = {|#0:new Harvest.Conversion.DetectionRule.CompositeDetection([
                                         new Harvest.Conversion.DetectionRule.ResourceDetection("#main")
                                     ])|};
                                 }
                             }
                             """,
            new DiagnosticResult(CompositeDetectionSpecificityAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("#main"));
    }
}
