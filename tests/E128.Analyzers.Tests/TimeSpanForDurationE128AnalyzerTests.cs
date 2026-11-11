using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimeSpanForDurationE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyWithTimeoutSeconds_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimeSpanForDurationAnalyzer>("""
                           public class Config
                           {
                               public int {|E128050:TimeoutSeconds|} { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParameterWithDelayMs_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimeSpanForDurationAnalyzer>("""
                           public class Service
                           {
                               public void Execute(int {|E128050:DelayMs|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullableIntDuration_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimeSpanForDurationAnalyzer>("""
                           public class Config
                           {
                               public int? {|E128050:Interval|} { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TimeSpanProperty_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<TimeSpanForDurationAnalyzer>("""
                           using System;
                           public class Config
                           {
                               public TimeSpan Timeout { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringProperty_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<TimeSpanForDurationAnalyzer>("""
                           public class Config
                           {
                               public string TimeoutSeconds { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LowercaseLocalParam_NoDiagnostic()
    {
        // Bare lowercase "min" does not trigger — uppercase start required
        return AnalyzerTestRunner.VerifyAsync<TimeSpanForDurationAnalyzer>("""
                           public class Service
                           {
                               public void Execute(int min) { }
                           }
                           """);
    }
}
