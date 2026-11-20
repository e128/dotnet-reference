using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128237: Reports a test that a mechanism other than the xUnit <c lang="csharp">Skip</c>
///     argument disables. An NUnit or MSTest <c lang="csharp">[Ignore]</c> attribute reports
///     unless its reason carries a tracked reference. A test method inside a literal
///     <c lang="csharp">#if false</c> region reports at every site, because that form
///     carries no reason at all.
/// </summary>
/// <remarks>
///     E128168 owns the xUnit <c lang="csharp">Skip</c> argument, so this rule does not read
///     it. The disabled-text walk reads only the literal <c lang="csharp">#if false</c>
///     condition: a condition the analyzer cannot decide stays silent, and an
///     <c lang="csharp">#else</c> or <c lang="csharp">#elif</c> branch ends the collection,
///     because that branch is active or condition dependent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForeignTestDisableAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128237";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "A test is disabled outside the xUnit Skip form",
        "This test is disabled outside the xUnit Skip form: use a Skip reason with a tracked reference, or delete the test",
        "E128-Testing",
        DiagnosticSeverity.Warning,
        true,
        "An xUnit Skip reason carries a tracked work-item reference, and E128168 checks it. " +
        "An [Ignore] attribute without one, and a test method inside #if false, leave a disabled test with no owner.");

    private static readonly ImmutableArray<string> IgnoreTypeNames =
    [
        "NUnit.Framework.IgnoreAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute",
    ];

    private static readonly ImmutableArray<string> TestTypeNames =
    [
        "Xunit.FactAttribute",
        "Xunit.TheoryAttribute",
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute",
    ];

    private static readonly ImmutableArray<string> TestAttributeNames =
    [
        "Fact",
        "FactAttribute",
        "Theory",
        "TheoryAttribute",
        "Test",
        "TestAttribute",
        "TestCase",
        "TestCaseAttribute",
        "TestMethod",
        "TestMethodAttribute",
        "DataTestMethod",
        "DataTestMethodAttribute",
    ];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            var ignoreTypes = CollectTypes(start.Compilation, IgnoreTypeNames);

            start.RegisterSyntaxTreeAction(AnalyzeTree);

            if (ignoreTypes.IsEmpty)
            {
                return;
            }

            var testTypes = CollectTypes(start.Compilation, TestTypeNames);

            start.RegisterSyntaxNodeAction(
                attributeContext => AnalyzeAttribute(attributeContext, testTypes, ignoreTypes),
                SyntaxKind.Attribute);
        });
    }

    private static ImmutableArray<INamedTypeSymbol> CollectTypes(
        Compilation compilation,
        ImmutableArray<string> metadataNames)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var metadataName in metadataNames)
        {
            builder.AddRange(MetadataTypes.Find(compilation, metadataName));
        }

        return builder.ToImmutable();
    }

    private static void AnalyzeAttribute(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> testTypes,
        ImmutableArray<INamedTypeSymbol> ignoreTypes)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (!IsIgnoreName(XunitTestHelper.SimpleName(attribute.Name)))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !MetadataTypes.Contains(ignoreTypes, constructor.ContainingType))
        {
            return;
        }

        if (attribute.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not { } method
            || !CarriesTestAttribute(method, testTypes, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (HasTrackedReason(attribute, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation()));
    }

    private static bool IsIgnoreName(string name)
    {
        return string.Equals(name, "Ignore", StringComparison.Ordinal)
               || string.Equals(name, "IgnoreAttribute", StringComparison.Ordinal);
    }

    private static bool CarriesTestAttribute(
        MethodDeclarationSyntax method,
        ImmutableArray<INamedTypeSymbol> testTypes,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var candidate in attributeList.Attributes)
            {
                if (semanticModel.GetSymbolInfo(candidate, cancellationToken).Symbol is IMethodSymbol constructor
                    && MetadataTypes.Contains(testTypes, constructor.ContainingType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasTrackedReason(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        if (attribute.ArgumentList?.Arguments.FirstOrDefault(static argument => argument.NameEquals is null) is not { } reason)
        {
            return false;
        }

        var constant = context.SemanticModel.GetConstantValue(reason.Expression, context.CancellationToken);

        if (!constant.HasValue || constant.Value is not string text)
        {
            return true;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(attribute.SyntaxTree);

        return TrackerReference.IsTracked(TrackerReference.GetPattern(options), text);
    }

    private static void AnalyzeTree(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        var collecting = false;
        var depth = 0;

        foreach (var trivia in root.DescendantTrivia())
        {
            var directive = trivia.GetStructure() as DirectiveTriviaSyntax;

            if (collecting)
            {
                collecting = UpdateCollection(directive, trivia, ref depth, context);
            }
            else if (directive is IfDirectiveTriviaSyntax ifDirective && IsLiteralFalseCondition(ifDirective))
            {
                collecting = true;
                depth = 1;
            }
        }
    }

    private static bool UpdateCollection(
        DirectiveTriviaSyntax? directive,
        SyntaxTrivia trivia,
        ref int depth,
        SyntaxTreeAnalysisContext context)
    {
        if (directive is null)
        {
            ReportDisabledText(context, trivia);
            return true;
        }

        if (directive.IsKind(SyntaxKind.IfDirectiveTrivia))
        {
            depth++;
            return true;
        }

        if (directive.IsKind(SyntaxKind.ElseDirectiveTrivia) || directive.IsKind(SyntaxKind.ElifDirectiveTrivia))
        {
            return depth > 1;
        }

        if (!directive.IsKind(SyntaxKind.EndIfDirectiveTrivia))
        {
            return true;
        }

        depth--;
        return depth > 0;
    }

    private static void ReportDisabledText(SyntaxTreeAnalysisContext context, SyntaxTrivia trivia)
    {
        if (!trivia.IsKind(SyntaxKind.DisabledTextTrivia))
        {
            return;
        }

        ReportDisabledTests(context, trivia);
    }

    private static bool IsLiteralFalseCondition(IfDirectiveTriviaSyntax directive)
    {
        return directive.Condition.IsKind(SyntaxKind.FalseLiteralExpression);
    }

    private static void ReportDisabledTests(SyntaxTreeAnalysisContext context, SyntaxTrivia trivia)
    {
        var text = context.Tree.GetText(context.CancellationToken).ToString(trivia.Span);

        const string wrapperPrefix = "class Wrapper\n{\n";
        var parsed = SyntaxFactory.ParseCompilationUnit(wrapperPrefix + text + "\n}");

        foreach (var method in parsed.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!CarriesTestAttributeName(method))
            {
                continue;
            }

            var start = trivia.SpanStart + method.SpanStart - wrapperPrefix.Length;
            context.ReportDiagnostic(Diagnostic.Create(Rule, Location.Create(context.Tree, new TextSpan(start, method.Span.Length))));
        }
    }

    private static bool CarriesTestAttributeName(MethodDeclarationSyntax method)
    {
        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var candidate in attributeList.Attributes)
            {
                if (TestAttributeNames.Contains(XunitTestHelper.SimpleName(candidate.Name), StringComparer.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
