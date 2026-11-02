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

    private const string BindingFlagsMetadataName = "System.Reflection.BindingFlags";

    private const string TypeMetadataName = "System.Type";

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
        context.RegisterCompilationStartAction(startContext =>
        {
            var bindingFlagsType = startContext.Compilation.GetTypeByMetadataName(BindingFlagsMetadataName);
            var typeType = startContext.Compilation.GetTypeByMetadataName(TypeMetadataName);
            if (bindingFlagsType is null || typeType is null)
            {
                return;
            }

            // A member access covers the enum member and the reflection call shape. An identifier
            // covers the type name in a typeof, a cast, or a declaration.
            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMemberAccess(nodeContext, bindingFlagsType, typeType),
                SyntaxKind.SimpleMemberAccessExpression);
            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeIdentifier(nodeContext, bindingFlagsType),
                SyntaxKind.IdentifierName);
        });
    }

    private static void AnalyzeMemberAccess(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol bindingFlagsType,
        INamedTypeSymbol typeType)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Most member accesses sit outside a test method, and this walk allocates nothing.
        // Resolve symbols only for the members that remain. An aliased receiver such as
        // `using BF = System.Reflection.BindingFlags; BF.Public` resolves too, so the check
        // stays text-free.
        if (!IsInsideTestMethod(memberAccess))
        {
            return;
        }

        var receiverSymbol = context.SemanticModel.GetSymbolInfo(
            memberAccess.Expression, context.CancellationToken).Symbol;
        if (SymbolEqualityComparer.Default.Equals(receiverSymbol, bindingFlagsType))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, memberAccess.Expression.GetLocation(), "BindingFlags"));
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!ReflectionMethods.Contains(methodName, StringComparer.Ordinal))
        {
            return;
        }

        // A type derived from System.Type, such as TypeBuilder, exposes the same reflection methods.
        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol method
            || !TypeSymbols.DerivesFrom(method.ContainingType, typeType))
        {
            return;
        }

        SyntaxNode reportNode = memberAccess.Parent is InvocationExpressionSyntax invocation
            ? invocation
            : memberAccess;

        context.ReportDiagnostic(Diagnostic.Create(Rule, reportNode.GetLocation(), methodName));
    }

    private static void AnalyzeIdentifier(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol bindingFlagsType)
    {
        var identifier = (IdentifierNameSyntax)context.Node;

        // The member-access action already reports the receiver of an enum member.
        if (identifier.Parent is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Expression == identifier)
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

        var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
        if (SymbolEqualityComparer.Default.Equals(symbol, bindingFlagsType))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), "BindingFlags"));
        }
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
