using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DynamicKeywordAnalyzerTests
{
    private static DiagnosticResult Expected()
    {
        return new DiagnosticResult("E128232", DiagnosticSeverity.Warning).WithLocation(0);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALocalVariableDeclaresDynamic()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class C
            {
                void M()
                {
                    {|#0:dynamic|} payload = "text";
                    var length = payload.Length;
                }
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAParameterTypeUsesDynamic()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class C
            {
                void M({|#0:dynamic|} payload)
                {
                    var text = payload.ToString();
                }
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAMethodReturnTypeUsesDynamic()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class C
            {
                {|#0:dynamic|} M() => "text";
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALambdaReturnTypeUsesDynamic()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class C
            {
                void M()
                {
                    var convert = {|#0:dynamic|} (string text) => text;
                }
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAGenericTypeArgumentUsesDynamic()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            using System.Collections.Generic;

            class C
            {
                private readonly List<{|#0:dynamic|}> _items = new();
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenACastTargetsDynamic()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class C
            {
                void M(object value)
                {
                    var payload = ({|#0:dynamic|})value;
                }
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnIsExpressionTestsDynamic()
    {
        // The is-dynamic form triggers the compiler's own warning about testing
        // dynamic compatibility, which this rule does not own.
        var options = new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.Errors };

        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class C
            {
                void M(object value)
                {
                    if (value is {|#0:dynamic|})
                    {
                    }
                }
            }
            """,
            Expected(),
            options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAForEachVariableUsesDynamic()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class C
            {
                void M(object[] values)
                {
                    foreach ({|#0:dynamic|} item in values)
                    {
                    }
                }
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenBindingFailsOnTheRightHandSide()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class C
            {
                void M()
                {
                    {|#0:dynamic|} payload = {|CS0103:Parse|}();
                }
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAUserTypeIsNamedDynamic()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class dynamic
            {
                public int Value => 1;
            }

            class C
            {
                private dynamic _field = new dynamic();

                dynamic M(dynamic parameter)
                {
                    return parameter;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAUsingAliasNamesDynamic()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            using dynamic = System.Text.StringBuilder;

            class C
            {
                void M()
                {
                    dynamic builder = new System.Text.StringBuilder();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAUsingDirectiveReferencesADynamicNamespace()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            using dynamic;

            namespace dynamic
            {
                class Shape
                {
                }
            }

            class C
            {
                private dynamic.Shape _shape = new dynamic.Shape();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAVariableIsNamedDynamic()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class C
            {
                void M()
                {
                    var dynamic = 1;
                    var two = dynamic + 1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenDynamicIsAMemberAccessReceiver()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicKeywordAnalyzer>(
            """
            class N
            {
                public int Result => 1;
            }

            class C
            {
                void M(N dynamic)
                {
                    var result = dynamic.Result;
                }
            }
            """);
    }
}
