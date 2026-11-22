using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128233: Reports a <c lang="csharp">Parallel.For(from, to, body)</c> call whose body runs one
///     delegate per item. A range partition runs one delegate per chunk and removes the per-item
///     delegate cost.
/// </summary>
/// <remarks>
///     The rule reports only the shapes its code fix repairs, plus an async body. A synchronous
///     body that awaits, runs a loop, mutates its parameter, or uses the name
///     <c lang="csharp">range</c> stays silent. The <c lang="csharp">For&lt;TLocal&gt;</c> overloads
///     and a user type with its own <c lang="csharp">For</c> stay silent as well.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParallelForPerItemBodyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128233";

    /// <summary>
    ///     The parameter name the code fix gives the range lambda. The fix reads this name.
    /// </summary>
    internal const string RangeParameterName = "range";

    /// <summary>
    ///     The member name the fix rewrites as a range loop. The fix reads this name.
    /// </summary>
    internal const string ForMethodName = "For";

    private const string ParallelMetadataName = "System.Threading.Tasks.Parallel";
    private const string PartitionerMetadataName = "System.Collections.Concurrent.Partitioner";
    private const string ActionMetadataName = "System.Action`1";
    private const string ActionWithStateMetadataName = "System.Action`2";
    private const string ParallelLoopStateMetadataName = "System.Threading.Tasks.ParallelLoopState";
    private const string ForEachMethodName = "ForEach";

    private const string RangeRemedy =
        "use 'Parallel.ForEach' over 'Partitioner.Create' so one delegate covers a range";

    private const string AsyncRemedy =
        "an async body binds as async void, so use 'Parallel.ForEachAsync'";

    private const int ForArgumentCount = 3;
    private const int BodyArgumentIndex = 2;

    internal const int StateBodyParameterCount = 2;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Parallel.For runs one delegate per item",
        "'Parallel.For' invokes the body once per index: {0}",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "Parallel.For schedules one delegate invocation for every index. Parallel.ForEach over a " +
        "range from Partitioner.Create runs one delegate per chunk and removes the per-item " +
        "delegate cost. An async body binds as async void, so ForEachAsync is the remedy.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var parallelType = compilationContext.Compilation.GetTypeByMetadataName(ParallelMetadataName);
            var partitionerType = compilationContext.Compilation.GetTypeByMetadataName(PartitionerMetadataName);
            var actionType = compilationContext.Compilation.GetTypeByMetadataName(ActionMetadataName);
            var actionWithStateType = compilationContext.Compilation.GetTypeByMetadataName(ActionWithStateMetadataName);
            var stateType = compilationContext.Compilation.GetTypeByMetadataName(ParallelLoopStateMetadataName);

            if (parallelType is null
                || partitionerType is null
                || actionType is null
                || actionWithStateType is null
                || stateType is null)
            {
                return;
            }

            if (!parallelType.GetMembers().OfType<IMethodSymbol>().Any(static method =>
                    string.Equals(method.Name, ForEachMethodName, StringComparison.Ordinal)))
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(
                    nodeContext,
                    parallelType,
                    actionType,
                    actionWithStateType,
                    stateType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol parallelType,
        INamedTypeSymbol actionType,
        INamedTypeSymbol actionWithStateType,
        INamedTypeSymbol stateType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.ArgumentList is null
            || invocation.ArgumentList.Arguments.Count is not ForArgumentCount
            || !CallsForMember(invocation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, parallelType)
            || !TryGetBodyShape(method, actionType, actionWithStateType, stateType, out var hasState))
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var bodyArgument = FindArgument(arguments, method, BodyArgumentIndex);
        if (bodyArgument is null)
        {
            return;
        }

        var kind = ClassifyBody(bodyArgument.Expression, hasState, stateType, context);
        if (kind is BodyKind.None)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            kind is BodyKind.Async ? AsyncRemedy : RangeRemedy));
    }

    private enum BodyKind
    {
        None = 0,
        Sync = 1,
        Async = 2,
    }

    internal static ArgumentSyntax? FindArgument(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        IMethodSymbol method,
        int parameterIndex)
    {
        var positional = 0;

        foreach (var argument in arguments)
        {
            if (argument.NameColon is { } nameColon)
            {
                if (ParameterIndex(method, nameColon.Name.Identifier.ValueText) == parameterIndex)
                {
                    return argument;
                }

                continue;
            }

            if (positional == parameterIndex)
            {
                return argument;
            }

            positional++;
        }

        return null;
    }

    private static int ParameterIndex(IMethodSymbol method, string name)
    {
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (string.Equals(method.Parameters[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool CallsForMember(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax { Name: { } name }
               && string.Equals(name.Identifier.ValueText, ForMethodName, StringComparison.Ordinal);
    }

    private static bool TryGetBodyShape(
        IMethodSymbol method,
        INamedTypeSymbol actionType,
        INamedTypeSymbol actionWithStateType,
        INamedTypeSymbol stateType,
        out bool hasState)
    {
        hasState = false;
        var parameters = method.Parameters;
        if (parameters.Length is not ForArgumentCount)
        {
            return false;
        }

        var itemType = parameters[0].Type;
        if (!IsIntOrLong(itemType)
            || !SymbolEqualityComparer.Default.Equals(parameters[1].Type, itemType)
            || parameters[BodyArgumentIndex].Type is not INamedTypeSymbol bodyType)
        {
            return false;
        }

        if (IsSingleBody(bodyType, actionType, itemType))
        {
            return true;
        }

        if (!IsStateBody(bodyType, actionWithStateType, itemType, stateType))
        {
            return false;
        }

        hasState = true;
        return true;
    }

    private static bool IsSingleBody(
        INamedTypeSymbol bodyType,
        INamedTypeSymbol actionType,
        ITypeSymbol itemType)
    {
        return SymbolEqualityComparer.Default.Equals(bodyType.ConstructedFrom, actionType)
               && bodyType.TypeArguments.Length is 1
               && SymbolEqualityComparer.Default.Equals(bodyType.TypeArguments[0], itemType);
    }

    private static bool IsStateBody(
        INamedTypeSymbol bodyType,
        INamedTypeSymbol actionWithStateType,
        ITypeSymbol itemType,
        INamedTypeSymbol stateType)
    {
        return SymbolEqualityComparer.Default.Equals(bodyType.ConstructedFrom, actionWithStateType)
               && bodyType.TypeArguments.Length is StateBodyParameterCount
               && SymbolEqualityComparer.Default.Equals(bodyType.TypeArguments[0], itemType)
               && SymbolEqualityComparer.Default.Equals(bodyType.TypeArguments[1], stateType);
    }

    internal static bool IsIntOrLong(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64;
    }

    private static BodyKind ClassifyBody(
        ExpressionSyntax expression,
        bool hasState,
        INamedTypeSymbol stateType,
        SyntaxNodeAnalysisContext context)
    {
        if (expression is AnonymousFunctionExpressionSyntax lambda)
        {
            if (IsAsyncLambda(lambda))
            {
                return BodyKind.Async;
            }

            return IsReportableLambda(lambda, hasState) ? BodyKind.Sync : BodyKind.None;
        }

        if (context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || !MethodMatchesBody(method, hasState, stateType))
        {
            return BodyKind.None;
        }

        return method.IsAsync && method.ReturnsVoid ? BodyKind.Async : BodyKind.Sync;
    }

    private static bool MethodMatchesBody(
        IMethodSymbol method,
        bool hasState,
        INamedTypeSymbol stateType)
    {
        var expectedParameters = hasState ? StateBodyParameterCount : 1;
        if (!method.ReturnsVoid
            || method.Parameters.Length != expectedParameters
            || !IsIntOrLong(method.Parameters[0].Type))
        {
            return false;
        }

        return !hasState
            || (method.Parameters[1].RefKind is RefKind.None
                && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, stateType));
    }

    private static bool IsReportableLambda(AnonymousFunctionExpressionSyntax lambda, bool hasState)
    {
        var parameters = LambdaParameters(lambda);
        if (parameters.Count != (hasState ? StateBodyParameterCount : 1))
        {
            return false;
        }

        foreach (var parameter in parameters)
        {
            if (string.Equals(parameter.Identifier.ValueText, RangeParameterName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        var parameterName = parameters[0].Identifier.ValueText;
        return lambda.Body is { } body && IsReportableBody(body, parameterName);
    }

    private static SeparatedSyntaxList<ParameterSyntax> LambdaParameters(
        AnonymousFunctionExpressionSyntax lambda)
    {
        return lambda switch
        {
            SimpleLambdaExpressionSyntax simple => SyntaxFactory.SingletonSeparatedList(simple.Parameter),
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters,
            _ => default,
        };
    }

    internal static bool IsAsyncLambda(AnonymousFunctionExpressionSyntax lambda)
    {
        return lambda switch
        {
            ParenthesizedLambdaExpressionSyntax parenthesized =>
                parenthesized.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            SimpleLambdaExpressionSyntax simple => simple.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            AnonymousMethodExpressionSyntax anonymous => anonymous.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            _ => false,
        };
    }

    private static bool IsReportableBody(SyntaxNode body, string parameterName)
    {
        if (MutatesParameter(body, parameterName))
        {
            return false;
        }

        if (BodyUsesRangeName(body))
        {
            return false;
        }

        switch (body)
        {
            case BlockSyntax { Statements.Count: > 0 } block:
                return !OwnBodyContainsLoop(block) && !OwnBodyContainsAwait(block);
            case BlockSyntax:
                return false;
            case ExpressionSyntax expression:
                return IsStatementExpression(expression);
            default:
                return false;
        }
    }

    private static bool MutatesParameter(SyntaxNode body, string parameterName)
    {
        foreach (var node in body.DescendantNodesAndSelf())
        {
            if (UpdatesParameter(node, parameterName)
                || PassesParameterByReference(node, parameterName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool UpdatesParameter(SyntaxNode node, string parameterName)
    {
        if (node is AssignmentExpressionSyntax assignment
            && IsParameterIdentifier(assignment.Left, parameterName))
        {
            return true;
        }

        if (node is PrefixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression,
            } prefix
            && IsParameterIdentifier(prefix.Operand, parameterName))
        {
            return true;
        }

        if (node is PostfixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression,
            } postfix
            && IsParameterIdentifier(postfix.Operand, parameterName))
        {
            return true;
        }

        return false;
    }

    private static bool PassesParameterByReference(SyntaxNode node, string parameterName)
    {
        return node is ArgumentSyntax
        {
            RefKindKeyword.RawKind: (int)SyntaxKind.RefKeyword or (int)SyntaxKind.OutKeyword,
        } argument
        && IsParameterIdentifier(argument.Expression, parameterName);
    }

    private static bool BodyUsesRangeName(SyntaxNode body)
    {
        foreach (var token in body.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken)
                && string.Equals(token.ValueText, RangeParameterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool OwnBodyContainsLoop(SyntaxNode body)
    {
        return body.DescendantNodes(StopAtNestedFunction)
            .OfType<StatementSyntax>()
            .Any(static statement => statement is ForStatementSyntax
                or ForEachStatementSyntax
                or ForEachVariableStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax);
    }

    private static bool OwnBodyContainsAwait(SyntaxNode body)
    {
        return body.DescendantNodes(StopAtNestedFunction).OfType<AwaitExpressionSyntax>().Any();
    }

    private static bool StopAtNestedFunction(SyntaxNode node)
    {
        return node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax;
    }

    private static bool IsParameterIdentifier(ExpressionSyntax expression, string parameterName)
    {
        return expression is IdentifierNameSyntax identifier
               && string.Equals(identifier.Identifier.ValueText, parameterName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     True for the expression shapes that stay valid when the code fix wraps the body
    ///     expression in one statement. The fix reads this table, so both sides accept one set.
    /// </summary>
    internal static bool IsStatementExpression(ExpressionSyntax expression)
    {
        return expression is InvocationExpressionSyntax
            or AssignmentExpressionSyntax
            or ObjectCreationExpressionSyntax
            or PrefixUnaryExpressionSyntax
        {
            RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression,
        }
            or PostfixUnaryExpressionSyntax
        {
            RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression,
        };
    }
}
