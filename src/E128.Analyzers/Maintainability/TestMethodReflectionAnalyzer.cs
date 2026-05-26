using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Maintainability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestMethodReflectionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128090";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Avoid reflection in test methods",
        "Test method uses {0} — consider testing through public API instead",
        "Maintainability",
        DiagnosticSeverity.Info,
        true);

    private static readonly ImmutableArray<string> ReflectionMethods =
        ["GetMethod", "GetProperty", "GetField"];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not IdentifierNameSyntax identifier)
        {
            return;
        }

        if (!string.Equals(identifier.Identifier.ValueText, "BindingFlags", StringComparison.Ordinal))
        {
            return;
        }

        if (!IsInsideTestMethod(identifier))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), "BindingFlags"));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!ReflectionMethods.Contains(methodName, StringComparer.Ordinal))
        {
            return;
        }

        if (!IsInsideTestMethod(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }

    private static bool IsInsideTestMethod(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is MethodDeclarationSyntax methodDecl)
            {
                return HasTestAttribute(methodDecl);
            }
        }

        return false;
    }

    private static bool HasTestAttribute(MethodDeclarationSyntax methodDecl)
    {
        foreach (var attributeList in methodDecl.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var attrName = attribute.Name.ToString();
                if (attrName is "Fact" or "Theory" or "Xunit.Fact" or "Xunit.Theory")
                {
                    return true;
                }
            }
        }

        return false;
    }
}
