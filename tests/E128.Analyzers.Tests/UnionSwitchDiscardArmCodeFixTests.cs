using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnionSwitchDiscardArmCodeFixTests
{
    private static Task RunAsync(
        string code,
        string fixedCode,
        DiagnosticResult[]? expected = null,
        DiagnosticResult[]? fixedExpected = null)
    {
        var test = new CSharpCodeFixTest<UnionSwitchDiscardArmAnalyzer, UnionSwitchDiscardArmCodeFixProvider, DefaultVerifier>
        {
            TestCode = code,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.Sources.Add(UnionTestRuntime.StubSource);
        test.FixedState.Sources.Add(UnionTestRuntime.StubSource);

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

        if (expected is not null)
        {
            test.ExpectedDiagnostics.AddRange(expected);
        }

        if (fixedExpected is not null)
        {
            test.FixedState.ExpectedDiagnostics.AddRange(fixedExpected);
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
    public Task ReplacesDiscardArmWithNullArm()
    {
        return RunAsync(
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
            """,
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
    public Task NoFix_WhenTheCatchAllArmIsGuarded()
    {
        return RunAsync(
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
            """,
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
                        case var _ when Allow(): return 3;
                    }

                    return 0;
                }
            }
            """,
            fixedExpected: [new DiagnosticResult(
                UnionSwitchDiscardArmAnalyzer.DiagnosticId,
                DiagnosticSeverity.Warning)
            .WithSpan(16, 18, 16, 23)
            .WithArguments("GreetingResult", "var")]);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCatchAllArmBindsAValue()
    {
        return RunAsync(
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
            """,
            UnionSource + """

            internal static class Switcher
            {
                internal static int Switch(GreetingResult result)
                {
                    switch (result)
                    {
                        case Greeting: return 1;
                        case GreetingError: return 2;
                        case var _: return 3;
                    }

                    return 0;
                }
            }
            """,
            fixedExpected: [new DiagnosticResult(
                UnionSwitchDiscardArmAnalyzer.DiagnosticId,
                DiagnosticSeverity.Warning)
            .WithSpan(14, 18, 14, 23)
            .WithArguments("GreetingResult", "var")]);
    }
}
