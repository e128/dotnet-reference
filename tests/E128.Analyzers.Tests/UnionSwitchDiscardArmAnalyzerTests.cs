using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnionSwitchDiscardArmAnalyzerTests
{
    private static Task VerifyAsync(string code, DiagnosticResult[]? expected = null)
    {
        var test = new CSharpAnalyzerTest<UnionSwitchDiscardArmAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var project = solution.GetProject(projectId);
            if (project?.ParseOptions is not CSharpParseOptions parseOptions
                || project.CompilationOptions is not CSharpCompilationOptions compilationOptions)
            {
                return solution;
            }

            var suppressions = compilationOptions.SpecificDiagnosticOptions
                .Add("RSEXPERIMENTAL006", ReportDiagnostic.Suppress);
            return solution
                .WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview))
                .WithProjectCompilationOptions(
                    projectId,
                    compilationOptions.WithSpecificDiagnosticOptions(suppressions));
        });

        test.TestState.Sources.Add(UnionTestRuntime.StubSource);

        if (expected is not null)
        {
            test.ExpectedDiagnostics.AddRange(expected);
        }

        return test.RunAsync(TestContext.Current.CancellationToken);
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
    public Task Reports_WhenASwitchExpressionEndsInADiscardArm()
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
                        {|E128220:_|} => 0
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenASwitchStatementEndsInADiscardArm()
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
                        case {|E128220:var _|}: return 3;
                    }

                    return 0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSwitchEndsInANullArm()
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
                        null => 0
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSwitchIsExhaustive()
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
                        GreetingError => 2
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGoverningTypeIsNotAUnion()
    {
        return VerifyAsync(
            """
            internal static class Switcher
            {
                internal static int Switch(int value)
                {
                    return value switch
                    {
                        0 => 1,
                        _ => 2
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenASwitchOverAClosedHierarchyEndsInADiscardArm()
    {
        return VerifyAsync(
            ClosedShapeSource + """

            internal static class Switcher
            {
                internal static int Switch(Shape shape)
                {
                    return shape switch
                    {
                        Circle => 1,
                        Square => 2,
                        {|E128220:_|} => 0
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAClosedHierarchySwitchEndsInANullArm()
    {
        return VerifyAsync(
            ClosedShapeSource + """

            internal static class Switcher
            {
                internal static int Switch(Shape shape)
                {
                    return shape switch
                    {
                        Circle => 1,
                        Square => 2,
                        null => 0
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenASwitchStatementEndsInAGuardedVarArm()
    {
        return VerifyAsync(
            UnionSource + """

            internal static class Switcher
            {
                internal static bool Allow() => true;

                internal static int Switch(GreetingResult result)
                {
                    switch (result)
                    {
                        case Greeting: return 1;
                        case GreetingError: return 2;
                        case {|E128220:var _|} when Allow(): return 3;
                    }

                    return 0;
                }
            }
            """);
    }

    private const string ClosedShapeSource =
        """
        internal closed class Shape
        {
        }

        internal sealed class Circle : Shape
        {
        }

        internal sealed class Square : Shape
        {
        }
        """;
}
