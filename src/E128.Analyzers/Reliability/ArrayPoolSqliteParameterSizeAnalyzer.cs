using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArrayPoolSqliteParameterSizeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128086";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "ArrayPool buffer used as SqliteParameter value without Size",
        "Set SqliteParameter.Size when passing an ArrayPool-rented buffer -- pooled buffers are oversized and will write garbage bytes to the BLOB column",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "ArrayPool<byte>.Shared.Rent() returns a buffer with Length >= requested size. Without explicit .Size, SQLite reads the full buffer including padding bytes, corrupting BLOB data.");

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
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "AddWithValue", StringComparison.Ordinal))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
        if (args.Count != 2)
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!IsSqliteParameterCollectionMethod(method))
        {
            return;
        }

        var valueArg = args[1].Expression;
        var argType = context.SemanticModel.GetTypeInfo(valueArg, context.CancellationToken).Type;
        if (argType is not IArrayTypeSymbol arrayType
            || arrayType.ElementType.SpecialType != SpecialType.System_Byte)
        {
            return;
        }

        if (!IsFromArrayPoolRent(valueArg, context.SemanticModel))
        {
            return;
        }

        var parameterVariable = GetAssignedVariable(invocation);
        if (parameterVariable is not null && HasSizeAssignment(parameterVariable, invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Value", StringComparison.Ordinal))
        {
            return;
        }

        var propertySymbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        if (propertySymbol is not IPropertySymbol property)
        {
            return;
        }

        if (!IsSqliteParameterType(property.ContainingType))
        {
            return;
        }

        var valueExpr = assignment.Right;
        var valueType = context.SemanticModel.GetTypeInfo(valueExpr, context.CancellationToken).Type;
        if (valueType is not IArrayTypeSymbol arrayType
            || arrayType.ElementType.SpecialType != SpecialType.System_Byte)
        {
            return;
        }

        if (!IsFromArrayPoolRent(valueExpr, context.SemanticModel))
        {
            return;
        }

        if (HasSizeAssignmentForMemberAccess(memberAccess.Expression, assignment))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation()));
    }

    private static bool IsSqliteParameterCollectionMethod(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        return containingType is not null
               && (string.Equals(containingType.Name, "SqliteParameterCollection", StringComparison.Ordinal)
                   || string.Equals(containingType.Name, "DbParameterCollection", StringComparison.Ordinal));
    }

    private static bool IsSqliteParameterType(INamedTypeSymbol type)
    {
        return type is not null
               && (string.Equals(type.Name, "SqliteParameter", StringComparison.Ordinal)
                   || string.Equals(type.Name, "DbParameter", StringComparison.Ordinal));
    }

    private static bool IsFromArrayPoolRent(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is not IdentifierNameSyntax identifier)
        {
            return false;
        }

        var symbol = model.GetSymbolInfo(identifier).Symbol;
        if (symbol is not ILocalSymbol local)
        {
            return false;
        }

        var enclosing = identifier.FirstAncestorOrSelf<BlockSyntax>();
        if (enclosing is null)
        {
            return false;
        }

        foreach (var statement in enclosing.Statements)
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

                return declarator.Initializer is EqualsValueClauseSyntax init
                       && IsArrayPoolRentCall(init.Value, model);
            }
        }

        return false;
    }

    private static bool IsArrayPoolRentCall(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        var symbolInfo = model.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return false;
        }

        if (!string.Equals(method.Name, "Rent", StringComparison.Ordinal))
        {
            return false;
        }

        var containingType = method.ContainingType;
        return containingType is not null
               && containingType.IsGenericType
               && string.Equals(containingType.ConstructedFrom.Name, "ArrayPool", StringComparison.Ordinal);
    }

    private static string? GetAssignedVariable(InvocationExpressionSyntax invocation)
    {
        return invocation.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } =>
                declarator.Identifier.ValueText,
            AssignmentExpressionSyntax { Left: IdentifierNameSyntax id } =>
                id.Identifier.ValueText,
            _ => null
        };
    }

    private static bool HasSizeAssignment(string variableName, SyntaxNode anchor)
    {
        var block = anchor.FirstAncestorOrSelf<BlockSyntax>();
        if (block is null)
        {
            return false;
        }

        var passedAnchor = false;
        foreach (var statement in block.Statements)
        {
            if (!passedAnchor)
            {
                if (statement.Span.Contains(anchor.Span))
                {
                    passedAnchor = true;
                }

                continue;
            }

            if (ContainsSizeAssignmentForVariable(statement, variableName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSizeAssignmentForVariable(SyntaxNode node, string variableName)
    {
        foreach (var descendant in node.DescendantNodes())
        {
            if (descendant is not AssignmentExpressionSyntax assignment)
            {
                continue;
            }

            if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Size", StringComparison.Ordinal))
            {
                continue;
            }

            if (memberAccess.Expression is IdentifierNameSyntax id
                && string.Equals(id.Identifier.ValueText, variableName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSizeAssignmentForMemberAccess(
        ExpressionSyntax parameterExpression,
        AssignmentExpressionSyntax anchor)
    {
        var block = anchor.FirstAncestorOrSelf<BlockSyntax>();
        if (block is null)
        {
            return false;
        }

        var parameterText = parameterExpression.ToString();
        var passedAnchor = false;

        foreach (var statement in block.Statements)
        {
            if (!passedAnchor)
            {
                if (statement.Span.Contains(anchor.Span))
                {
                    passedAnchor = true;
                }

                continue;
            }

            foreach (var descendant in statement.DescendantNodes())
            {
                if (descendant is not AssignmentExpressionSyntax assignment)
                {
                    continue;
                }

                if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Size", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(memberAccess.Expression.ToString(), parameterText, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
