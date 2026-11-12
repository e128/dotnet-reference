using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnionValueReadAnalyzerTests
{
    private static Task VerifyAsync(string code, DiagnosticResult[]? expected = null)
    {
        var test = new CSharpAnalyzerTest<UnionValueReadAnalyzer, DefaultVerifier>
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
