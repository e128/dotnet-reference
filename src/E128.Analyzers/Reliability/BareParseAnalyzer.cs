using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BareParseAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128089";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Avoid bare .Parse() — use TryParse to handle invalid input",
        "Call {0}.Parse() without TryParse — use TryParse to avoid FormatException on untrusted input",
        "Reliability",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Parse", StringComparison.Ordinal))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return;
        }

        // Check if the containing type has a TryParse method
        if (!HasTryParse(containingType))
        {
            return;
        }

        // Check if we're inside a try/catch that catches FormatException or Exception
        if (IsInsideTryCatchForFormatException(invocation, context))
        {
            return;
        }

        var typeName = containingType.ToDisplayString();
        var shortName = typeName.Substring(typeName.LastIndexOf('.') + 1);
        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), shortName));
    }

    private static bool HasTryParse(INamedTypeSymbol containingType)
    {
        // Special case: Enum.Parse<T>() — Enum does have TryParse<T>() too
        // But we want to flag it as well
        var members = containingType.GetMembers("TryParse");
        if (members.IsEmpty)
        {
            return false;
        }

        foreach (var member in members)
        {
            if (member is IMethodSymbol method &&
                method.IsStatic &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                method.Parameters.Length >= 1)
            {
                // Found a static bool TryParse(...) method
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideTryCatchForFormatException(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context)
    {
        for (var current = invocation.Parent; current is not null; current = current.Parent)
        {
            if (current is TryStatementSyntax tryStatement)
            {
                foreach (var catchClause in tryStatement.Catches)
                {
                    if (IsFormatExceptionOrException(catchClause, context))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsFormatExceptionOrException(
        CatchClauseSyntax catchClause,
        SyntaxNodeAnalysisContext context)
    {
        if (catchClause.Declaration is null)
        {
            return true;
        }

        var typeName = catchClause.Declaration.Type;
        if (typeName is null)
        {
            return true;
        }

        var typeInfo = context.SemanticModel.GetTypeInfo(typeName, context.CancellationToken);
        if (typeInfo.Type is null)
        {
            return false;
        }

        var fullName = typeInfo.Type.ToDisplayString();
        return fullName is "System.FormatException" or "System.Exception";
    }
}
