using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimeSpanForDurationE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TimeSpanForDurationAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyWithTimeoutSeconds_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
                           public class Service
                           {
                               public void Execute(int min) { }
                           }
                           """);
    }
}
