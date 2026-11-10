using System.Threading.Tasks;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed partial class LowValueTestAnalyzerTests
{
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
                                   Assert.Equal("user_example.com", KeyNormalizer.Sanitize("user@example.com"));
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

    [Fact]
    [Trait("Category", "CI")]
    public Task DuplicateCoverage_ReportsNothing_WhenSetupConstructorDiffers()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Seed
                           {
                               private readonly int _offset;

                               public Seed(int offset) => _offset = offset;

                               public int Adjust(int left, int right) => left + right + _offset;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ProduceTheFirstTotal()
                               {
                                   var seed = new Seed(0);
                                   var total = seed.Adjust(1, 2);
                                   Assert.Equal(3, total);
                               }

                               [Fact]
                               public void Should_ProduceTheSecondTotal()
                               {
                                   var seed = new Seed(1);
                                   var total = seed.Adjust(1, 2);
                                   Assert.Equal(3, total);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DuplicateCoverage_ReportsNothing_WhenTheMethodUnderTestDiffers()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Numbers
                           {
                               public Numbers Parse(int value) => this;

                               public int Negate() => 5;

                               public int Halve() => 8;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ProduceTheFirstResult()
                               {
                                   var numbers = new Numbers();
                                   var result = numbers.Parse(7).Negate();
                                   Assert.Equal(9, result);
                               }

                               [Fact]
                               public void Should_ProduceTheSecondResult()
                               {
                                   var numbers = new Numbers();
                                   var result = numbers.Parse(7).Halve();
                                   Assert.Equal(9, result);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SelfFulfillingExpected_ReportsNothing_WhenOperandsAreDistinctElementsOfOneResult()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using Xunit;

                           public static class PageRank
                           {
                               public static Dictionary<string, double> Compute() => new();
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_ProduceEqualRanks()
                               {
                                   var ranks = PageRank.Compute();

                                   Assert.Equal(ranks["A"], ranks["B"], 6);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoAssertion_ReportsNothing_WhenTheProductionCallReturnsVoid()
    {
        return VerifyAsync("""
                           using Xunit;

                           public sealed class Validator
                           {
                               public void Validate(string value) { }
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void Should_AcceptTheValue()
                               {
                                   new Validator().Validate("alpha");
                               }
                           }
                           """);
    }
}
