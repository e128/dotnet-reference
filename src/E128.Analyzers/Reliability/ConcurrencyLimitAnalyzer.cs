using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128040: Detects concurrency primitives initialized with zero or negative limits.
///     Covers SemaphoreSlim(0), ParallelOptions.MaxDegreeOfParallelism = 0,
///     and Channel.CreateBounded(0).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConcurrencyLimitAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128040";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Concurrency limit must be positive",
        "Concurrency limit is {0} — zero or negative values produce cryptic runtime errors. Use a positive integer (or -1 for MaxDegreeOfParallelism to mean unlimited).",
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
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>
    ///     Handles <c>new SemaphoreSlim(N)</c> — first constructor arg must be positive.
    /// </summary>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (!TryGetArgumentList(context.Node, out var argumentList, out var nodeForLocation)
            || argumentList is null || nodeForLocation is null)
        {
            return;
        }

#pragma warning disable RCS9004
        if (argumentList.Arguments.Count == 0)
#pragma warning restore RCS9004
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol constructor)
        {
            return;
        }

        if (constructor.ContainingType is not { } containingType || !IsSemaphoreSlim(containingType))
        {
            return;
        }

        var firstArg = argumentList.Arguments[0].Expression;
        if (!TryGetConstantInt(context.SemanticModel, firstArg, out var value))
        {
            return;
        }

        if (value <= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, nodeForLocation.GetLocation(), value));
        }
    }

    private static bool TryGetArgumentList(
        SyntaxNode node,
        out ArgumentListSyntax? argumentList,
        out SyntaxNode? nodeForLocation)
    {
        if (node is ObjectCreationExpressionSyntax objectCreation
            && objectCreation.ArgumentList is { } objArgList)
        {
            argumentList = objArgList;
            nodeForLocation = objectCreation;
            return true;
        }

        if (node is ImplicitObjectCreationExpressionSyntax implicitCreation
            && implicitCreation.ArgumentList is { } implArgList)
        {
            argumentList = implArgList;
            nodeForLocation = implicitCreation;
            return true;
        }

        argumentList = default;
        nodeForLocation = default;
        return false;
    }

    /// <summary>
    ///     Handles <c>MaxDegreeOfParallelism = N</c> property assignment — must be positive or -1.
    /// </summary>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (assignment.Left is not IdentifierNameSyntax identifier)
        {
            return;
        }

        if (!string.Equals(identifier.Identifier.ValueText, "MaxDegreeOfParallelism", StringComparison.Ordinal))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol property)
        {
            return;
        }

        if (!IsParallelOptions(property.ContainingType))
        {
            return;
        }

        if (!TryGetConstantInt(context.SemanticModel, assignment.Right, out var value))
        {
            return;
        }

        // -1 means "unlimited" — a valid value for MaxDegreeOfParallelism.
        if (value == -1)
        {
            return;
        }

        if (value <= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.Right.GetLocation(), value));
        }
    }

    /// <summary>
    ///     Handles <c>Channel.CreateBounded&lt;T&gt;(N)</c> — capacity must be positive.
    /// </summary>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "CreateBounded", StringComparison.Ordinal))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
#pragma warning disable RCS9004
        if (args.Count == 0)
#pragma warning restore RCS9004
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!IsChannelCreateBounded(method))
        {
            return;
        }

        var firstArg = args[0].Expression;
        if (!TryGetConstantInt(context.SemanticModel, firstArg, out var value))
        {
            return;
        }

        if (value <= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), value));
        }
    }

    private static bool TryGetConstantInt(SemanticModel model, ExpressionSyntax expression, out int value)
    {
        value = 0;

        if (expression is PrefixUnaryExpressionSyntax prefix
            && prefix.IsKind(SyntaxKind.UnaryMinusExpression)
            && prefix.Operand is LiteralExpressionSyntax innerLiteral
            && innerLiteral.Token.Value is int innerVal)
        {
            value = -innerVal;
            return true;
        }

        if (expression is LiteralExpressionSyntax literal && literal.Token.Value is int literalVal)
        {
            value = literalVal;
            return true;
        }

        var constantValue = model.GetConstantValue(expression);
        if (constantValue.HasValue && constantValue.Value is int constVal)
        {
            value = constVal;
            return true;
        }

        return false;
    }

    private static bool IsSemaphoreSlim(INamedTypeSymbol type)
    {
        return string.Equals(type.Name, "SemaphoreSlim", StringComparison.Ordinal)
               && type.ContainingNamespace is { Name: "Threading" }
               && type.ContainingNamespace.ContainingNamespace is { Name: "System" };
    }

    private static bool IsParallelOptions(INamedTypeSymbol type)
    {
        return type is not null
               && string.Equals(type.Name, "ParallelOptions", StringComparison.Ordinal)
               && type.ContainingNamespace is { Name: "Tasks" }
               && type.ContainingNamespace.ContainingNamespace is { Name: "Threading" }
               && type.ContainingNamespace.ContainingNamespace.ContainingNamespace is { Name: "System" };
    }

    private static bool IsChannelCreateBounded(IMethodSymbol method)
    {
        if (!string.Equals(method.Name, "CreateBounded", StringComparison.Ordinal))
        {
            return false;
        }

        var containingType = method.ContainingType;
        return containingType is not null
               && string.Equals(containingType.Name, "Channel", StringComparison.Ordinal)
               && containingType.ContainingNamespace is { Name: "Channels" }
               && containingType.ContainingNamespace.ContainingNamespace is { Name: "Threading" }
               && containingType.ContainingNamespace.ContainingNamespace.ContainingNamespace is { Name: "System" };
    }
}
