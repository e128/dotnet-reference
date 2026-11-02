using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimeSpanForDurationE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<TimeSpanForDurationAnalyzer, TimeSpanForDurationCodeFixProvider>(source, fixedCode);

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

    [Fact]
    [Trait("Category", "CI")]
    public Task ParameterWithDefaultValue_KeepsTheSource()
    {
        const string source = """
                              using System;
                              public class Service
                              {
                                  public void Run(double {|E128050:DelayMs|} = 0) { }
                              }
                              """;

        // No fix is offered, so the fixed state keeps the diagnostic. The marker states that
        // expectation instead of asserting the diagnostic disappears.
        return VerifyFixAsync(source, """
                                       using System;
                                       public class Service
                                       {
                                           public void Run(double {|E128050:DelayMs|} = 0) { }
                                       }
                                       """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenThePropertyHasANumericInitializer()
    {
        const string source = """
                              using System;
                              public class Service
                              {
                                  public int {|E128050:TimeoutMs|} { get; init; } = 5000;
                              }
                              """;

        // Changing the type alone would leave `= 5000` next to TimeSpan, which does not compile.
        return VerifyFixAsync(source, source);
    }
}
