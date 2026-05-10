using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PoolRentCapacityGuardAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128070";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Pool Rent() capacity must be bounded",
        "Rent({0}) may allocate an excessive buffer — guard with Math.Min(value, cap) to prevent OOM",
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

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Rent", StringComparison.Ordinal))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
        if (args.Count != 1)
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!IsPoolRentMethod(method))
        {
            return;
        }

        var argument = args[0].Expression;

        if (IsGuardedByMathMin(argument, context.SemanticModel))
        {
            return;
        }

        if (IsSafeLiteral(argument, context.SemanticModel))
        {
            return;
        }

        var rentSpan = TextSpan.FromBounds(
            memberAccess.Name.SpanStart, invocation.ArgumentList.Span.End);
        var rentLocation = Location.Create(invocation.SyntaxTree, rentSpan);

        context.ReportDiagnostic(Diagnostic.Create(Rule, rentLocation, argument.ToString()));
    }

    private static bool IsPoolRentMethod(IMethodSymbol method)
    {
        if (!string.Equals(method.Name, "Rent", StringComparison.Ordinal))
        {
            return false;
        }

        if (method.Parameters.Length != 1)
        {
            return false;
        }

        if (method.Parameters[0].Type.SpecialType != SpecialType.System_Int32)
        {
            return false;
        }

        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        var typeName = containingType.Name;

        return typeName.Contains("Pool")
               || typeName.Contains("pool")
               || (containingType.IsGenericType
                   && string.Equals(containingType.ConstructedFrom.Name, "ArrayPool", StringComparison.Ordinal));
    }

    private static bool IsGuardedByMathMin(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is InvocationExpressionSyntax innerInvocation)
        {
            var innerSymbol = model.GetSymbolInfo(innerInvocation).Symbol;
            if (innerSymbol is IMethodSymbol innerMethod
                && string.Equals(innerMethod.Name, "Min", StringComparison.Ordinal)
                && string.Equals(innerMethod.ContainingType?.Name, "Math", StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (expression is IdentifierNameSyntax identifier)
        {
            var symbol = model.GetSymbolInfo(identifier).Symbol;
            if (symbol is ILocalSymbol local)
            {
                return IsLocalAssignedFromMathMin(local, identifier, model);
            }
        }

        return false;
    }

    private static bool IsLocalAssignedFromMathMin(
        ILocalSymbol local,
        IdentifierNameSyntax usage,
        SemanticModel model)
    {
        var enclosingMethod = usage.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
        if (enclosingMethod?.Body is null)
        {
            return false;
        }

        foreach (var statement in enclosingMethod.Body.Statements)
        {
            if (statement is not LocalDeclarationStatementSyntax localDecl)
            {
                continue;
            }

            foreach (var declarator in localDecl.Declaration.Variables)
            {
                if (!string.Equals(declarator.Identifier.ValueText, local.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (declarator.Initializer?.Value is InvocationExpressionSyntax initInvocation)
                {
                    var initSymbol = model.GetSymbolInfo(initInvocation).Symbol;
                    if (initSymbol is IMethodSymbol initMethod
                        && string.Equals(initMethod.Name, "Min", StringComparison.Ordinal)
                        && string.Equals(initMethod.ContainingType?.Name, "Math", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsSafeLiteral(ExpressionSyntax expression, SemanticModel model)
    {
        var constantValue = model.GetConstantValue(expression);

        return constantValue is { HasValue: true, Value: int value }
               && value != int.MaxValue
               && value >= 0;
    }
}
