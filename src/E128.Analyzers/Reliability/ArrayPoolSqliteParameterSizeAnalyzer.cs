using System;
using System.Collections.Concurrent;
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

        // The Size-assignment index depends only on the block. Build it once per block and share
        // it across every candidate in that block.
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var sizeAssignments =
                new ConcurrentDictionary<BlockSyntax, ImmutableArray<(string Receiver, int Position)>>();

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, sizeAssignments),
                SyntaxKind.InvocationExpression);
            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeAssignment(nodeContext, sizeAssignments),
                SyntaxKind.SimpleAssignmentExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<BlockSyntax, ImmutableArray<(string Receiver, int Position)>> sizeAssignments)
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
        if (parameterVariable is not null && HasLaterSizeAssignment(parameterVariable, invocation, sizeAssignments))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static void AnalyzeAssignment(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<BlockSyntax, ImmutableArray<(string Receiver, int Position)>> sizeAssignments)
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

        if (HasLaterSizeAssignment(memberAccess.Expression.ToString(), assignment, sizeAssignments))
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

        // Resolve the declaration through the symbol. The old code rescanned every statement in
        // the enclosing block for each candidate.
        foreach (var reference in local.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree == model.SyntaxTree
                && reference.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: { } value }
                && IsArrayPoolRentCall(value, model))
            {
                return true;
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

    // A Size assignment counts when it sits in a statement after the anchor statement.
    private static bool HasLaterSizeAssignment(
        string receiver,
        SyntaxNode anchor,
        ConcurrentDictionary<BlockSyntax, ImmutableArray<(string Receiver, int Position)>> cache)
    {
        var block = anchor.FirstAncestorOrSelf<BlockSyntax>();
        if (block is null)
        {
            return false;
        }

        var anchorStatement = anchor.FirstAncestorOrSelf<StatementSyntax>();
        var anchorEnd = anchorStatement?.Span.End ?? anchor.Span.End;

        foreach (var (candidateReceiver, position) in GetSizeAssignments(block, cache))
        {
            if (position > anchorEnd
                && string.Equals(candidateReceiver, receiver, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<(string Receiver, int Position)> GetSizeAssignments(
        BlockSyntax block,
        ConcurrentDictionary<BlockSyntax, ImmutableArray<(string Receiver, int Position)>> cache)
    {
        return cache.GetOrAdd(block, static b =>
        {
            var builder = ImmutableArray.CreateBuilder<(string Receiver, int Position)>();

            foreach (var node in b.DescendantNodes())
            {
                if (node is AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax memberAccess } assignment
                    && string.Equals(memberAccess.Name.Identifier.ValueText, "Size", StringComparison.Ordinal))
                {
                    builder.Add((memberAccess.Expression.ToString(), assignment.SpanStart));
                }
            }

            return builder.ToImmutable();
        });
    }
}
