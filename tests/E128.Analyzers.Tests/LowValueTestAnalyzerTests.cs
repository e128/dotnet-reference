using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LowValueTestAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<LowValueTestAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithXunit
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareSize_ReportsSuggestion_WhenBodyIsThreeStatementsWithOneAssertion()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_Add|}()
                               {
                                   var left = 1;
                                   var right = 2;
                                   Assert.Equal(3, left + right);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NameMirror_ReportsSuggestion_WhenTestNameRepeatsTargetMethodName()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Parser
                           {
                               public string Parse(string value) => value;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_Parse|}()
                               {
                                   var value = new Parser().Parse("alpha");
                                   Assert.Equal("alpha", value);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullOnly_ReportsNothing_WhenSoleAssertionIsNotNull()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Parser
                           {
                               public string Parse(string value) => value;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ReturnValue()
                               {
                                   var parser = new Parser();
                                   var value = parser.Parse("alpha");
                                   Assert.NotNull(value);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToStringComparison_ReportsNothing_WhenSoleAssertionTargetsToString()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class Calculator
                           {
                               public static int Add(int left, int right) => left + right;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_FormatSum()
                               {
                                   Assert.Equal("3", Calculator.Add(1, 2).ToString());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoAssertion_ReportsSuggestion_WhenBodyCallsProductionAndAwaitsNothing()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Parser
                           {
                               public void Parse(string value) { }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_RunWorkflow|}()
                               {
                                   new Parser().Parse("alpha");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoAssertion_ReportsNothing_WhenBodyAwaitsAVerifierHelper()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           using Xunit;

                           public static class Verifier
                           {
                               public static Task Verify(string value)
                               {
                                   _ = value;
                                   return Task.CompletedTask;
                               }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public async Task Should_ReturnResult()
                               {
                                   await Verifier.Verify("alpha");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SingleRowTheory_ReportsSuggestion_WhenTheoryCarriesOneDataRow()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Subject
                           {
                               [Theory]
                               [InlineData(1)]
                               public void {|E128107:Should_Double|}(int value)
                               {
                                   var doubled = value * 2;
                                   Assert.Equal(2, doubled);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralEcho_ReportsSuggestion_WhenAssertionComparesLiteralWithLiteralOnlyCall()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class Calculator
                           {
                               public static int Add(int left, int right) => left + right;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_SumTwoValues|}()
                               {
                                   Assert.Equal(3, Calculator.Add(1, 2));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MultipleConditions_ReportWarning_WhenNameMirrorAndLiteralEchoMatch()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class Calculator
                           {
                               public static int Add(int left, int right) => left + right;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128108:Should_Add|}()
                               {
                                   Assert.Equal(3, Calculator.Add(1, 2));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExceptionMessageLock_ReportsSuggestion_WhenAssertionPinsExceptionMessage()
    {
        return VerifyAsync("""
                           using System;
                           using Xunit;

                           public sealed class Parser
                           {
                               public void Parse(string value)
                               {
                                   if (value.Length == 0)
                                   {
                                       throw new InvalidOperationException("Value must not be empty");
                                   }
                               }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_RejectEmptyValue|}()
                               {
                                   var parser = new Parser();

                                   var exception = Assert.Throws<InvalidOperationException>(() => parser.Parse(""));

                                   Assert.Equal("Value must not be empty", exception.Message);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MockVerifyOnly_ReportsSuggestion_WhenSoleAssertionIsVerifyWithLambda()
    {
        return VerifyAsync("""
                           using System;
                           using System.Linq.Expressions;
                           using System.Threading.Tasks;
                           using Xunit;

                           public interface ISender
                           {
                               void Send(string value);
                           }

                           public sealed class SenderSpy
                           {
                               public Task VerifyAsync(Expression<Action<ISender>> expression)
                               {
                                   _ = expression;
                                   return Task.CompletedTask;
                               }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public async Task {|E128107:Should_DispatchTheValue|}()
                               {
                                   var spy = new SenderSpy();

                                   await spy.VerifyAsync(sender => sender.Send("alpha"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LogAssert_ReportsSuggestion_WhenSoleAssertionTargetsLogger()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           using Xunit;

                           public sealed class RecordingLogger
                           {
                               public Task VerifyLoggedAsync(string message)
                               {
                                   _ = message;
                                   return Task.CompletedTask;
                               }
                           }

                           public sealed class Service
                           {
                               private readonly RecordingLogger logger;

                               public Service(RecordingLogger logger)
                               {
                                   this.logger = logger;
                               }

                               public void Run()
                               {
                                   _ = this.logger;
                               }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public async Task {|E128107:Should_LogStartup|}()
                               {
                                   var logger = new RecordingLogger();
                                   var service = new Service(logger);

                                   service.Run();

                                   await logger.VerifyLoggedAsync("startup");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorPassthrough_ReportsSuggestion_WhenLiteralIsReadBackOffConstructedObject()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Parser
                           {
                               public Parser(int limit)
                               {
                                   this.Limit = limit;
                               }

                               public int Limit { get; }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_StoreLimit|}()
                               {
                                   var parser = new Parser(3);
                                   Assert.Equal(3, parser.Limit);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InternalsReachIn_ReportsSuggestion_WhenAssertionReadsInternalMember()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           using Xunit;

                           [assembly: InternalsVisibleTo("E128.Analyzers.Tests")]

                           public static class InternalPolicy
                           {
                               internal static int Limit => 5;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_ReadInternalLimit|}()
                               {
                                   Assert.Equal(5, InternalPolicy.Limit);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SelfFulfillingExpected_ReportsSuggestion_WhenExpectedDerivesFromTheSameReceiver()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Parser
                           {
                               public string Value { get; set; } = string.Empty;

                               public string GetValue() => this.Value;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_CompareStoredAndFetched|}()
                               {
                                   var parser = new Parser();
                                   Assert.Equal(parser.GetValue(), parser.Value);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MagicConstantEcho_ReportsSuggestion_WhenExpectedLiteralAppearsInProductionBody()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class Limits
                           {
                               public static int Max() => 42;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_ReturnConfiguredBound|}()
                               {
                                   Assert.Equal(42, Limits.Max());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OrderLock_ReportsSuggestion_WhenExactOrderIsPinnedOnASetResult()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using Xunit;

                           public static class NumberSet
                           {
                               public static HashSet<int> Values { get; } = new HashSet<int> { 1, 2, 3 };
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_PreserveContents|}()
                               {
                                   Assert.Equal(new[] { 1, 2, 3 }, NumberSet.Values);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DuplicateCoverage_ReportsSuggestion_WhenTwoTestsShareMethodAndLiteralArguments()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class Calculator
                           {
                               public static int Add(int left, int right) => left + right;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_ProducePositiveValue|}()
                               {
                                   var sum = Calculator.Add(1, 2);
                                   Assert.NotEqual(0, sum);
                               }

                               [Fact]
                               public void {|E128107:Should_ProduceNonZeroValue|}()
                               {
                                   var sum = Calculator.Add(1, 2);
                                   Assert.NotEqual(0, sum);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoAssertion_ReportsNothing_WhenBodyCallsAThrowBasedAssertion()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Architecture
                           {
                               public void Check() { }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_EnforceTheLayout()
                               {
                                   var target = new Architecture();

                                   target.Check();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareSize_ReportsNothing_WhenBodyCallsProductionCode()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Greeter
                           {
                               public string Greet(string name) => "Hello, " + name + "!";
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ReturnTheGreeting()
                               {
                                   var greeter = new Greeter();
                                   var result = greeter.Greet("Claude");
                                   Assert.Equal("Hello, Claude!", result);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NameMirror_ReportsNothing_WhenTestNameAlsoStatesTheBehavior()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Parser
                           {
                               public string Parse(string value) => value;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Parse_ReturnsTheValue()
                               {
                                   var parser = new Parser();
                                   var value = parser.Parse("alpha");
                                   Assert.Equal("alpha", value);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MagicConstantEcho_ReportsNothing_WhenEchoedLiteralIsNotReturned()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Limits
                           {
                               public static string Resolve(string? name)
                               {
                                   var resolved = name ?? "World";

                                   return resolved;
                               }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ReturnTheResolvedName()
                               {
                                   string? requested = null;
                                   Assert.Equal("World", Limits.Resolve(requested));
                               }
                           }
                           """);
    }
}
