using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnionSwitchDiscardArmAnalyzerBranchCoverageMoreTests
{
    private const string UnionSource =
        """
        enum Greeting { Hello }
        enum GreetingError { NotSubscribed, NotAllowed }
        union GreetingResult(Greeting, GreetingError)
        {
        }
        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<UnionSwitchDiscardArmAnalyzer>(
            code,
            options: new AnalyzerTestOptions
            {
                LanguageVersion = LanguageVersion.Preview,
                Sources = [UnionTestRuntime.StubSource],
                SuppressedDiagnosticIds = ["RSEXPERIMENTAL006"],
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenASwitchExpressionEndsInAVarArm()
    {
        return VerifyAsync(
            UnionSource + """

            internal static class Switcher
            {
                internal static int Switch(GreetingResult result)
                {
                    return result switch
                    {
                        Greeting => 1,
                        GreetingError => 2,
                        {|E128220:var other|} => 0
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenASwitchStatementGovernsANonUnionType()
    {
        return VerifyAsync(
            """
            internal static class Switcher
            {
                internal static int Switch(int value)
                {
                    switch (value)
                    {
                        case 0: return 1;
                        default: return 2;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenASwitchStatementEndsInANullArm()
    {
        return VerifyAsync(
            UnionSource + """

            internal static class Switcher
            {
                internal static int Switch(GreetingResult result)
                {
                    switch (result)
                    {
                        case Greeting: return 1;
                        case GreetingError: return 2;
                        case null: return 0;
                    }

                    return 3;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenASwitchStatementHasNoCatchAllArm()
    {
        return VerifyAsync(
            UnionSource + """

            internal static class Switcher
            {
                internal static int Switch(GreetingResult result)
                {
                    switch (result)
                    {
                        case Greeting: return 1;
                        case GreetingError: return 2;
                    }

                    return 3;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGoverningTypeIsAnArrayOrATypeParameter()
    {
        return VerifyAsync(
            """
            internal static class Switcher
            {
                internal static int Switch(int[] values)
                {
                    return values switch
                    {
                        [] => 0,
                        _ => 1
                    };
                }

                internal static int Generic<T>(T value)
                {
                    return value switch
                    {
                        _ => 0
                    };
                }
            }
            """);
    }
}
