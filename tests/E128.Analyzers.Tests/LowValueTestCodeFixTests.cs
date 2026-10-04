using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LowValueTestCodeFixTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyAsync(string source, string fixedCode)
    {
        // The analyzer defers every report to the compilation end action, so no diagnostic exists at
        // the local stage. The framework check for a local diagnostic is skipped for that reason.
        return new CSharpCodeFixTest<LowValueTestAnalyzer, LowValueTestCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = Net100WithXunit,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesMethod_WhenDiagnosticIsReported()
    {
        return VerifyAsync(
            """
            using Xunit;

            public static class Calculator
            {
                public static int Echo(int value) => value;
            }

            public sealed class Subject
            {
                public static int Zero() => 0;

                [Fact]
                public void {|E128107:Should_SumTwoValues|}()
                {
                    Assert.Equal(1, Calculator.Echo(1));
                }
            }
            """,
            """
            using Xunit;

            public static class Calculator
            {
                public static int Echo(int value) => value;
            }

            public sealed class Subject
            {
                public static int Zero() => 0;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesMethod_WhenAttributeIsQualified()
    {
        return VerifyAsync(
            """
            using Xunit;

            public static class Calculator
            {
                public static int Echo(int value) => value;
            }

            public sealed class Subject
            {
                public static int Zero() => 0;

                [Xunit.Fact]
                public void {|E128107:Should_SumTwoValues|}()
                {
                    Assert.Equal(1, Calculator.Echo(1));
                }
            }
            """,
            """
            using Xunit;

            public static class Calculator
            {
                public static int Echo(int value) => value;
            }

            public sealed class Subject
            {
                public static int Zero() => 0;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_KeepsClosingBraceIndentation_WhenTypeIsNested()
    {
        return VerifyAsync(
            """
            using Xunit;

            public static class Calculator
            {
                public static int Echo(int value) => value;
            }

            public sealed class Outer
            {
                public sealed class Subject
                {
                    public static int Zero() => 0;

                    [Fact]
                    public void {|E128107:Should_SumTwoValues|}()
                    {
                        Assert.Equal(1, Calculator.Echo(1));
                    }
                }
            }
            """,
            """
            using Xunit;

            public static class Calculator
            {
                public static int Echo(int value) => value;
            }

            public sealed class Outer
            {
                public sealed class Subject
                {
                    public static int Zero() => 0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task CodeFix_RegistersNoAction_WhenNodeIsNotATestMethod()
    {
        const string code = """
                            public sealed class Subject
                            {
                                public static int Zero() => 0;
                            }
                            """;

        Assert.Empty(await RegisterFixesAsync(code, "Zero"));
    }

    private static async Task<List<CodeAction>> RegisterFixesAsync(string code, string methodName)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(code));

        var root = await document.GetSyntaxRootAsync();
        var method = root!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => string.Equals(m.Identifier.ValueText, methodName, StringComparison.Ordinal));

        var descriptor = new DiagnosticDescriptor(
            LowValueTestAnalyzer.SuggestionDiagnosticId,
            "title",
            "message",
            "Testing",
            DiagnosticSeverity.Info,
            true);
        var diagnostic = Diagnostic.Create(descriptor, method.Identifier.GetLocation());

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await new LowValueTestCodeFixProvider().RegisterCodeFixesAsync(context);
        return actions;
    }
}
