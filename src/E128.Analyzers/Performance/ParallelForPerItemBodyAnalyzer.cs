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
///     The rule reports only the shapes its code fix repairs. A body that awaits or runs a loop, a
///     body whose parameter it mutates or passes by reference, and a body that uses the name
///     <c lang="csharp">range</c> stay silent. The <c lang="csharp">For&lt;TLocal&gt;</c> overloads,
///     the long overload, and a user type with its own <c lang="csharp">For</c> stay silent as well.
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
    private const string ForEachMethodName = "ForEach";

    // The overload shape the fix rewrites: fromInclusive, toExclusive, body.
    private const int ForArgumentCount = 3;
    private const int BodyArgumentIndex = 2;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Parallel.For runs one delegate per item",
        "'Parallel.For' invokes the body once per index: use 'Parallel.ForEach' over 'Partitioner.Create' so one delegate covers a range",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "Parallel.For schedules one delegate invocation for every index. Parallel.ForEach over a " +
        "range from Partitioner.Create runs one delegate per chunk and removes the per-item " +
        "delegate cost.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // The fix needs Partitioner.Create and Parallel.ForEach. Gate on the remedy, so a
        // compilation without it gets no report.
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var parallelType = compilationContext.Compilation.GetTypeByMetadataName(ParallelMetadataName);
            var partitionerType = compilationContext.Compilation.GetTypeByMetadataName(PartitionerMetadataName);
            var actionType = compilationContext.Compilation.GetTypeByMetadataName(ActionMetadataName);

            if (parallelType is null || partitionerType is null || actionType is null)
            {
                return;
            }

            if (!parallelType.GetMembers().OfType<IMethodSymbol>().Any(static method =>
                    string.Equals(method.Name, ForEachMethodName, StringComparison.Ordinal)))
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, parallelType, actionType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol parallelType,
        INamedTypeSymbol actionType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.ArgumentList is null
            || invocation.ArgumentList.Arguments.Count is not ForArgumentCount
            || !CallsForMember(invocation))
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;

        // The fix emits positional arguments only, so a call with named arguments stays silent.
        if (arguments.Any(static argument => argument.NameColon is not null))
        {
            return;
        }

        // A user type with its own For, the long overload, the Action<int, ParallelLoopState>
        // overload, and the For<TLocal> overloads all reach this bind and stay silent.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, parallelType)
            || !IsIntBodyOverload(method, actionType))
        {
            return;
        }

        if (arguments[BodyArgumentIndex].Expression is not AnonymousFunctionExpressionSyntax lambda
            || !IsReportableLambda(lambda))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool CallsForMember(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax { Name: { } name }
               && string.Equals(name.Identifier.ValueText, ForMethodName, StringComparison.Ordinal);
    }

    // The fix builds a for loop with the (int, int, Action<int>) shape. Partitioner.Create takes
    // int ranges, so every other overload stays silent.
    private static bool IsIntBodyOverload(IMethodSymbol method, INamedTypeSymbol actionType)
    {
        var parameters = method.Parameters;
        if (parameters.Length is not ForArgumentCount)
        {
            return false;
        }

        if (parameters[0].Type.SpecialType is not SpecialType.System_Int32
            || parameters[1].Type.SpecialType is not SpecialType.System_Int32)
        {
            return false;
        }

        return parameters[BodyArgumentIndex].Type is INamedTypeSymbol bodyType
               && bodyType.TypeArguments.Length is 1
               && bodyType.TypeArguments[0].SpecialType is SpecialType.System_Int32
               && SymbolEqualityComparer.Default.Equals(bodyType.ConstructedFrom, actionType);
    }

    private static bool IsReportableLambda(AnonymousFunctionExpressionSyntax lambda)
    {
        // The fix rewrites lambda syntax only. A method group and an anonymous method stay silent.
        var parameter = LambdaParameter(lambda);
        if (parameter is null)
        {
            return false;
        }

        var parameterName = parameter.Identifier.ValueText;

        // The fix declares a lambda parameter named "range". A body parameter with that name would
        // collide with the declared range parameter, so the rewrite would not compile.
        if (string.Equals(parameterName, RangeParameterName, StringComparison.Ordinal))
        {
            return false;
        }

        // The range overload takes a synchronous delegate. An async lambda binds as an async void
        // delegate, and the fix does not model that shape.
        if (Is(lambda))
        {
            return false;
        }

        return lambda.Body is { } body && IsReportableBody(body, parameterName);
    }

    private static ParameterSyntax? LambdaParameter(AnonymousFunctionExpressionSyntax lambda)
    {
        return lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters: { Count: 1 } parameters }
                => parameters[0],
            _ => null,
        };
    }

    private static bool IsReportableBody(SyntaxNode body, string parameterName)
    {
        // The fix declares the parameter as the loop variable. A body that mutates the parameter,
        // or passes it by reference, would mutate the loop variable instead. The scan crosses
        // nested lambdas on purpose: a nested lambda that mutates the captured parameter has the
        // same effect after the rewrite.
        if (MutatesParameter(body, parameterName))
        {
            return false;
        }

        // Any other use of the name "range" in the body, as a reference or a declaration, would
        // collide with the declared range parameter. The scan is wider than strictly needed, so a
        // member access such as "state.range" also stays silent.
        if (BodyUsesRangeName(body))
        {
            return false;
        }

        switch (body)
        {
            case BlockSyntax { Statements.Count: > 0 } block:
                // A body that already runs a loop per item gains nothing from a range partition.
                return !OwnBodyContainsLoop(block) && !OwnBodyContainsAwait(block);
            case BlockSyntax:
                // An empty body does no per-item work, so the report has no value.
                return false;
            case ExpressionSyntax expression:
                // An expression body becomes one statement of the rewritten for loop, so it must
                // fit a statement position. A read such as "i => items[i]" does not.
                return IsStatementExpression(expression);
            default:
                return false;
        }
    }

    private static bool Is(AnonymousFunctionExpressionSyntax lambda)
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

    // True for an assignment to the parameter, or an increment or decrement of it.
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

    // True for an argument that passes the parameter by reference.
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

    // A loop or an await inside a nested lambda stays nested after the rewrite, so that body walk
    // stops at the nested boundary. The mutation scan above does not.
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
