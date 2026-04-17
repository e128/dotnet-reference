using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimeSpanForDurationE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<TimeSpanForDurationAnalyzer, TimeSpanForDurationCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyIntToTimeSpan_Fixed()
    {
        const string source = """
                              using System;
                              public class Config
                              {
                                  public int {|E128050:TimeoutSeconds|} { get; set; }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 public class Config
                                 {
                                     public TimeSpan TimeoutSeconds { get; set; }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParameterDoubleToTimeSpan_Fixed()
    {
        const string source = """
                              using System;
                              public class Service
                              {
                                  public void Run(double {|E128050:DelayMs|}) { }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 public class Service
                                 {
                                     public void Run(TimeSpan DelayMs) { }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
