using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnionValueReadAnalyzerTests
{
    private static Task VerifyAsync(string code, DiagnosticResult[]? expected = null)
    {
        return AnalyzerTestRunner.VerifyAsync<UnionValueReadAnalyzer>(
            code,
            expected,
            new AnalyzerTestOptions
            {
                LanguageVersion = LanguageVersion.Preview,
                Sources = [UnionTestRuntime.StubSource],
                SuppressedDiagnosticIds = ["RSEXPERIMENTAL006"],
            });
    }

    private const string UnionSource =
        """
        enum Greeting { Hello }
        enum GreetingError { NotSubscribed, NotAllowed }
        union GreetingResult(Greeting, GreetingError)
        {
        }
        """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAValuePropertyIsRead()
    {
        return VerifyAsync(
            UnionSource + """

            internal static class Reader
            {
                internal static int Read(GreetingResult result)
                {
                    return {|E128221:result.Value|}.GetHashCode();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAUnionIsCastOutsideItsCaseType()
    {
        return VerifyAsync(
            UnionSource + """

            internal static class Reader
            {
                internal static object Read(GreetingResult result)
                {
                    return {|E128221:(object)result|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueIsReadThroughAPattern()
    {
        return VerifyAsync(
            UnionSource + """

            internal static class Reader
            {
                internal static int Read(GreetingResult result)
                {
                    return result switch
                    {
                        Greeting => 1,
                        GreetingError => 2
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsNotAUnion()
    {
        return VerifyAsync(
            """
            using System.Collections.Generic;

            internal static class Reader
            {
                internal static int Read()
                {
                    var values = new Dictionary<int, int>();
                    return values.Count + 1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAMemberNamedValueIsNotTheUnionProperty()
    {
        return VerifyAsync(
            """
            internal static class Reader
            {
                internal static int Read(Carrier carrier)
                {
                    return carrier.Value;
                }
            }

            internal sealed class Carrier
            {
                internal int Value { get; }
            }
            """);
    }
}
