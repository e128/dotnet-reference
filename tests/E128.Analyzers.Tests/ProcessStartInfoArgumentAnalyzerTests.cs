using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessStartInfoArgumentAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InterpolatedStringArguments_WithoutArgumentList()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessStartInfoArgumentAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string value)
                               {
                                   var startInfo = new ProcessStartInfo
                                   {
                                       FileName = "tool",
                                       {|E128091:Arguments = $"-x \"{value}\""|}
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_EmptyStringArguments()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessStartInfoArgumentAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M()
                               {
                                   var startInfo = new ProcessStartInfo
                                   {
                                       FileName = "tool",
                                       Arguments = string.Empty
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenArgumentListPopulated()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessStartInfoArgumentAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string value)
                               {
                                   var startInfo = new ProcessStartInfo
                                   {
                                       FileName = "tool",
                                       ArgumentList = { "-x", value }
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ArgumentsAssignedAfterConstruction()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessStartInfoArgumentAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string value)
                               {
                                   var startInfo = new ProcessStartInfo { FileName = "tool" };
                                   {|E128091:startInfo.Arguments = $"-x \"{value}\""|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenArgumentsAssignedOnAnUnrelatedType()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessStartInfoArgumentAnalyzer>("""
                           using System.Diagnostics;
                           class Wrapper { public string Arguments { get; set; } = string.Empty; }

                           class C
                           {
                               void M(string value)
                               {
                                   var wrapper = new Wrapper();
                                   wrapper.Arguments = $"-x \"{value}\"";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenArgumentsAssignedWithAnEmptyString()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessStartInfoArgumentAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M()
                               {
                                   var startInfo = new ProcessStartInfo { FileName = "tool" };
                                   startInfo.Arguments = string.Empty;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_BareIdentifierArguments_RealRegressionShape()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessStartInfoArgumentAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               string BuildArguments(string value) => $"-m \"{value}\"";

                               void M(string value)
                               {
                                   var arguments = BuildArguments(value);
                                   var startInfo = new ProcessStartInfo
                                   {
                                       FileName = "tool",
                                       {|E128091:Arguments = arguments|}
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TargetTypedCreation_Reports()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessStartInfoArgumentAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string value)
                               {
                                   ProcessStartInfo startInfo = new()
                                   {
                                       FileName = "tool",
                                       {|E128091:Arguments = value|}
                                   };
                               }
                           }
                           """);
    }
}
