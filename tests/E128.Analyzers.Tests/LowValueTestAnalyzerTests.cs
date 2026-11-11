using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed partial class LowValueTestAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithXunit };

    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    [Fact]
    [Trait("Category", "CI")]
    public Task BareSize_ReportsSuggestion_WhenBodyIsThreeStatementsWithOneAssertion()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NameMirror_ReportsSuggestion_WhenTestNameRepeatsTargetMethodName()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullOnly_ReportsNothing_WhenSoleAssertionIsNotNull()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToStringComparison_ReportsNothing_WhenSoleAssertionTargetsToString()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoAssertion_ReportsSuggestion_WhenBodyCallsProductionAndAwaitsNothing()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
                           using Xunit;

                           public sealed class Parser
                           {
                               public int Parse(string value) => 0;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_RunWorkflow|}()
                               {
                                   new Parser().Parse("alpha");
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoAssertion_ReportsNothing_WhenBodyAwaitsAVerifierHelper()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SingleRowTheory_ReportsSuggestion_WhenTheoryCarriesOneDataRow()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralEcho_ReportsSuggestion_WhenAssertionComparesLiteralWithLiteralOnlyCall()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
                           using Xunit;

                           public static class Calculator
                           {
                               public static int Echo(int value) => value;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_ReturnTheInputValue|}()
                               {
                                   Assert.Equal(1, Calculator.Echo(1));
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MultipleConditions_ReportWarning_WhenNameMirrorAndLiteralEchoMatch()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
                           using Xunit;

                           public static class Calculator
                           {
                               public static int Echo(int value) => value;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128108:Should_Echo|}()
                               {
                                   Assert.Equal(1, Calculator.Echo(1));
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExceptionMessageLock_ReportsSuggestion_WhenAssertionPinsExceptionMessage()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExceptionMessageLock_ReportsNothing_WhenAssertionContainsASubstringOfTheMessage()
    {
        // A substring check pins a documented hint, which is the contract, not a leak. Only an exact
        // equality check locks the whole message.
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
                           using System;
                           using Xunit;

                           public sealed class Parser
                           {
                               public void Parse(string value)
                               {
                                   if (value.Length == 0)
                                   {
                                       throw new InvalidOperationException("Set BRAVE_API_KEY before parsing");
                                   }
                               }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ReportTheMissingKey()
                               {
                                   var parser = new Parser();

                                   var exception = Assert.Throws<InvalidOperationException>(() => parser.Parse(""));

                                   Assert.Contains("BRAVE_API_KEY", exception.Message, StringComparison.Ordinal);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MockVerifyOnly_ReportsSuggestion_WhenSoleAssertionIsVerifyWithLambda()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LogAssert_ReportsSuggestion_WhenSoleAssertionTargetsLogger()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorPassthrough_ReportsSuggestion_WhenLiteralIsReadBackOffConstructedObject()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SelfFulfillingExpected_ReportsSuggestion_WhenBothOperandsAreTheSameExpression()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
                           using Xunit;

                           public sealed class Parser
                           {
                               public string Value { get; set; } = string.Empty;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_CompareTheValue|}()
                               {
                                   var parser = new Parser();
                                   Assert.Equal(parser.Value, parser.Value);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MagicConstantEcho_ReportsSuggestion_WhenExpectedLiteralAppearsInProductionBody()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OrderLock_ReportsSuggestion_WhenExactOrderIsPinnedOnASetResult()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DuplicateCoverage_ReportsSuggestion_WhenTwoTestsShareMethodAndLiteralArguments()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoAssertion_ReportsNothing_WhenBodyCallsAThrowBasedAssertion()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareSize_ReportsNothing_WhenBodyCallsProductionCode()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NameMirror_ReportsNothing_WhenTestNameAlsoStatesTheBehavior()
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>("""
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
                           """, options: Options);
    }
}
