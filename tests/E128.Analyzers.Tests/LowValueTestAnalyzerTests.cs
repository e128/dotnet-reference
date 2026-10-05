using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var options = (CSharpCompilationOptions)project.CompilationOptions!;
            return solution.WithProjectCompilationOptions(
                projectId,
                options.WithSpecificDiagnosticOptions(
                    options.SpecificDiagnosticOptions
                        .SetItem("E128107", ReportDiagnostic.Info)
                        .SetItem("E128108", ReportDiagnostic.Warn)));
        });
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
    public Task ExceptionMessageLock_ReportsNothing_WhenAssertionContainsASubstringOfTheMessage()
    {
        // A substring check pins a documented hint, which is the contract, not a leak. Only an exact
        // equality check locks the whole message.
        return VerifyAsync("""
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
    public Task LiteralEcho_ReportsNothing_WhenLiteralSitsInsideTheEchoedCall()
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
                               public void Should_ReturnTheComputedSum()
                               {
                                   var left = 1;
                                   var right = 2;
                                   var expected = left + right;
                                   Assert.Equal(expected, Calculator.Add(1, 2));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DuplicateCoverage_ReportsNothing_WhenOneTestRepeatsTheSameCall()
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
                               public void Should_ReturnTheSameSum()
                               {
                                   var first = Calculator.Add(1, 2);
                                   var second = Calculator.Add(1, 2);
                                   Assert.Equal(first, second);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DuplicateCoverage_ReportsNothing_WhenTestsShareOnlyASeedingCall()
    {
        return VerifyAsync("""
                           using System.Linq;
                           using Xunit;

                           public static class Parser
                           {
                               public static int SplitCode(int width) => width;
                               public static int SplitQuote(int width) => width;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void SplitCodeBlock()
                               {
                                   var width = Enumerable.Range(1, 100).Sum();
                                   Assert.True(Parser.SplitCode(width) > 1);
                               }

                               [Fact]
                               public void SplitQuoteBlock()
                               {
                                   var width = Enumerable.Range(1, 100).Sum();
                                   Assert.True(Parser.SplitQuote(width) > 1);
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

    [Fact]
    [Trait("Category", "CI")]
    public Task LogAssert_ReportsNothing_WhenReceiverNameMerelyContainsLog()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class DialogProbe
                           {
                               public void Verify(string value)
                               {
                                   _ = value;
                               }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ConfirmTheAnswer()
                               {
                                   var dialog = new DialogProbe();

                                   dialog.Verify("alpha");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorPassthrough_ReportsNothing_WhenConstructorTakesNoMatchingLiteral()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using Xunit;

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_StartEmpty()
                               {
                                   var items = new List<int>();
                                   Assert.Equal(0, items.Count);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralEcho_ReportsNothing_WhenAssertionReadsALibraryAccessor()
    {
        return VerifyAsync("""
                           using Xunit;

                           public interface IElement
                           {
                               string GetAttribute(string name);
                           }

                           public sealed class Element : IElement
                           {
                               public string GetAttribute(string name) => name;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ReturnTheSource()
                               {
                                   IElement element = new Element();
                                   Assert.Equal("image.png", element.GetAttribute("src"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DuplicateCoverage_ReportsNothing_WhenTestsShareOnlyALibraryAccessor()
    {
        return VerifyAsync("""
                           using Xunit;

                           public interface IReader
                           {
                               string Read(string key);
                           }

                           public sealed class Reader : IReader
                           {
                               public string Read(string key) => key;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ReadTheName()
                               {
                                   IReader reader = new Reader();
                                   Assert.Equal("alpha", reader.Read("name"));
                               }

                               [Fact]
                               public void Should_ReadTheValue()
                               {
                                   IReader reader = new Reader();
                                   Assert.Equal("alpha", reader.Read("name"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralEcho_Reports_WhenExpectedLiteralRepeatsTheCallInput()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128107:Should_ReturnTheName|}()
                               {
                                   Assert.Equal("abc", Subject.Normalize("abc"));
                               }

                               public static string Normalize(string value) => value.ToUpperInvariant();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralEcho_ReportsNothing_WhenExpectedLiteralDiffersFromTheCallInput()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class KeyNormalizer
                           {
                               public static string Sanitize(string value) => value.Replace("@", "_");
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ReplaceTheAtSign()
                               {
                                   Assert.Equal("user_domain.com", KeyNormalizer.Sanitize("user@domain.com"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DuplicateCoverage_ReportsNothing_WhenTestsCheckDifferentResultsOfTheSameCall()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Builder
                           {
                               public int Count => 0;

                               public int this[string key] => 0;

                               public void Add(string key, int value)
                               {
                               }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_CountTheEntries()
                               {
                                   var builder = new Builder();
                                   builder.Add("a", 1);
                                   Assert.Equal(1, builder.Count);
                               }

                               [Fact]
                               public void Should_ReadTheEntry()
                               {
                                   var builder = new Builder();
                                   builder.Add("a", 1);
                                   Assert.Equal(1, builder["a"]);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MagicConstantEcho_ReportsNothing_WhenLiteralComesFromATestClassHelper()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class ReportWriter
                           {
                               public string Write(string team) => $"<h1>{team}</h1>";
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_PutTheTeamNameInTheTitle()
                               {
                                   var model = CreateModel();
                                   var html = new ReportWriter().Write(model);

                                   Assert.Contains("Falcons", html);
                               }

                               private static string CreateModel() => "Falcons";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralEcho_ReportsNothing_WhenExpectedArgumentInvokesProduction()
    {
        return VerifyAsync("""
                           using System;

                           using Xunit;

                           public sealed class Comparer
                           {
                               public static int Hash(string value) => value.Length;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_HashBothFormsTheSameWay()
                               {
                                   Assert.Equal(Comparer.Hash((string)(object)"test"), Comparer.Hash("test"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LiteralEcho_ReportsNothing_WhenARealAssertionAccompaniesTheEcho()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class TagCanonicalizer
                           {
                               public static string Canonicalize(string value) => value;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_StayIdempotentForACanonicalTag()
                               {
                                   Assert.Equal("chronograph", TagCanonicalizer.Canonicalize("chronograph"));
                                   Assert.Equal("chronograph", TagCanonicalizer.Canonicalize("chrono display"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareSize_ReportsNothing_WhenBodyReadsAProductionProperty()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Pool
                           {
                               public static Pool Shared { get; } = new Pool();
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ReturnTheSameInstance()
                               {
                                   var first = Pool.Shared;
                                   var second = Pool.Shared;

                                   Assert.Same(first, second);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoAssertion_ReportsNothing_WhenBodyCallsATestClassHelper()
    {
        return VerifyAsync("""
                           using System;

                           using Xunit;

                           public static class Corpus
                           {
                               public static void Validate(string root)
                               {
                                   if (root.Length == 0)
                                   {
                                       throw new ArgumentException("empty", nameof(root));
                                   }
                               }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_AcceptAnExistingRoot()
                               {
                                   AssertDoesNotThrow("/corpus");
                               }

                               private static void AssertDoesNotThrow(string root)
                               {
                                   Corpus.Validate(root);
                               }
                           }
                           """);
    }
}
