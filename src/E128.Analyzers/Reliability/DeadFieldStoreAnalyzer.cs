using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128250: A statement assigns a private field and a later statement in the same block
///     overwrites it before any read.
/// </summary>
/// <remarks>
///     The rule tracks simple assignments in one statement list. Any call, object creation, or
///     control-flow statement clears the tracked writes, because it can read the field or skip the
///     next write. A read of the field also clears its tracked write. The final write of a block
///     stays silent, because the field lives after the method returns. A compound assignment and a
///     field that another type declares stay out. The rule carries no code fix, because the first
///     right side can carry a side effect.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeadFieldStoreAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128250";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Dead store to a private field",
        "The assignment to '{0}' is overwritten before any read",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Remove the first assignment, or move the work that reads the value before the second assignment.");

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
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not BaseMethodDeclarationSyntax declaration
            || declaration.Body is not { } body
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.Constructor } method)
        {
            return;
        }

        AnalyzeBody(body, method.ContainingType, context);
    }

    private static void AnalyzeBody(
        BlockSyntax body,
        INamedTypeSymbol containingType,
        SyntaxNodeAnalysisContext context)
    {
        foreach (var block in body.DescendantNodesAndSelf()
                     .OfType<BlockSyntax>()
                     .Where(block => !IsInsideDeferredBody(block, body)))
        {
            AnalyzeBlock(block, containingType, context);
        }
    }

    /// <summary>
    ///     True for a block inside a lambda or a local function. That body runs later, so this
    ///     statement list says nothing about its order.
    /// </summary>
    private static bool IsInsideDeferredBody(BlockSyntax block, BlockSyntax methodBody)
    {
        for (var ancestor = block.Parent; ancestor is not null && ancestor != methodBody; ancestor = ancestor.Parent)
        {
            if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeBlock(
        BlockSyntax block,
        INamedTypeSymbol containingType,
        SyntaxNodeAnalysisContext context)
    {
        var pending = new Dictionary<ISymbol, AssignmentExpressionSyntax>(SymbolEqualityComparer.Default);

        foreach (var statement in block.Statements)
        {
            if (TryGetFieldAssignment(statement, containingType, context) is { } assignment)
            {
                TrackAssignment(assignment, pending, context);
                continue;
            }

            if (EscapesOrReads(statement, pending, context))
            {
                continue;
            }

            pending.Clear();
        }
    }

    private static void TrackAssignment(
        AssignmentExpressionSyntax assignment,
        Dictionary<ISymbol, AssignmentExpressionSyntax> pending,
        SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
            is not IFieldSymbol field)
        {
            return;
        }

        if (ContainsCall(assignment.Right))
        {
            pending.Clear();
            pending[field] = assignment;
            return;
        }

        foreach (var readField in ReadFields(assignment.Right, context))
        {
            _ = pending.Remove(readField);
        }

        if (pending.TryGetValue(field, out var deadStore))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, deadStore.GetLocation(), field.Name));
        }

        pending[field] = assignment;
    }

    private static bool EscapesOrReads(
        StatementSyntax statement,
        Dictionary<ISymbol, AssignmentExpressionSyntax> pending,
        SyntaxNodeAnalysisContext context)
    {
        if (ContainsCall(statement))
        {
            pending.Clear();
            return true;
        }

        if (statement is not LocalDeclarationStatementSyntax)
        {
            return false;
        }

        foreach (var readField in ReadFields(statement, context))
        {
            _ = pending.Remove(readField);
        }

        return true;
    }

    private static AssignmentExpressionSyntax? TryGetFieldAssignment(
        StatementSyntax statement,
        INamedTypeSymbol containingType,
        SyntaxNodeAnalysisContext context)
    {
        if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
            || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            return null;
        }

        var field = context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
            as IFieldSymbol;
        return field is not null && IsPrivateFieldOfType(field, containingType) ? assignment : null;
    }

    private static bool IsPrivateFieldOfType(IFieldSymbol field, INamedTypeSymbol containingType)
    {
        return field.DeclaredAccessibility is Accessibility.Private
            && SymbolEqualityComparer.Default.Equals(field.ContainingType, containingType);
    }

    /// <summary>
    ///     True when the node contains a call or an object creation. Either can read the field
    ///     through an instance method, so the tracked writes stop being provably dead.
    /// </summary>
    private static bool ContainsCall(SyntaxNode node)
    {
        return node.DescendantNodesAndSelf().Any(static descendant =>
            descendant is InvocationExpressionSyntax or ObjectCreationExpressionSyntax);
    }

    private static IEnumerable<ISymbol> ReadFields(SyntaxNode node, SyntaxNodeAnalysisContext context)
    {
        foreach (var identifier in node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
            if (symbol is IFieldSymbol)
            {
                yield return symbol;
            }
        }
    }
}
