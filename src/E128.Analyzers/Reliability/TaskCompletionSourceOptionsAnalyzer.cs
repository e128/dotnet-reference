using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128159: Reports a <c lang="csharp">TaskCompletionSource</c> that the caller creates
///     without <c lang="csharp">TaskCreationOptions.RunContinuationsAsynchronously</c>. Without the
///     flag the continuation of the awaiting task runs inside the thread that calls
///     <c lang="csharp">SetResult</c>.
/// </summary>
/// <remarks>
///     The rule reads a constant options value only. A computed value carries no decision, so the
///     call stays silent. A second shape reports a throwing completer on a source stored in a
///     field, because two callers can then complete it once each.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskCompletionSourceOptionsAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128159";

    internal const string OptionsTypeName = "TaskCreationOptions";

    internal const string RunContinuationsFlag = "TaskCreationOptions.RunContinuationsAsynchronously";

    internal const int RunContinuationsValue = 0x40;

    private const string InterlockedTypeName = "System.Threading.Interlocked";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "TaskCompletionSource without RunContinuationsAsynchronously",
        "Pass TaskCreationOptions.RunContinuationsAsynchronously to the TaskCompletionSource",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A TaskCompletionSource without TaskCreationOptions.RunContinuationsAsynchronously runs " +
        "the awaiting continuation inside the thread that completes the task. That thread then " +
        "executes foreign code, which can deadlock or stall a pool thread.");

    private static readonly DiagnosticDescriptor SharedCompletionRule = new(
        DiagnosticId,
        "SetResult on a shared TaskCompletionSource",
        "Use TrySetResult or TrySetException on a TaskCompletionSource stored in a field",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A TaskCompletionSource stored in a field outlives one caller. A second caller that calls " +
        "SetResult, SetException, or SetCanceled throws InvalidOperationException. The TrySet " +
        "forms return false instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Rule, SharedCompletionRule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var sources = new List<INamedTypeSymbol>();
        AddDefinitions(context.Compilation, "System.Threading.Tasks.TaskCompletionSource`1", sources);
        AddDefinitions(context.Compilation, "System.Threading.Tasks.TaskCompletionSource", sources);

        if (sources.Count == 0)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeCreation(ctx, sources),
            SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeCreation(ctx, sources),
            SyntaxKind.ImplicitObjectCreationExpression);
        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeSharedCompletion(ctx, sources),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeSharedCompletion(
        SyntaxNodeAnalysisContext context,
        List<INamedTypeSymbol> sources)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !IsThrowingCompleter(memberAccess.Name.Identifier.ValueText))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol method
            || !IsCompletionSourceType(method.ContainingType, sources))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(memberAccess.Expression, context.CancellationToken)
                .Symbol is not IFieldSymbol)
        {
            return;
        }

        // An Interlocked exchange over a field elects one winning caller, so the completion runs at
        // most once and the throwing form cannot see a second caller.
        if (IsGuardedByInterlocked(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(SharedCompletionRule, invocation.GetLocation()));
    }

    private static bool IsGuardedByInterlocked(
        InvocationExpressionSyntax completion,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        for (var node = completion.Parent; node is not null; node = node.Parent)
        {
            if (node is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax)
            {
                return false;
            }

            var condition = node switch
            {
                IfStatementSyntax ifStatement => ifStatement.Condition,
                ConditionalExpressionSyntax conditional => conditional.Condition,
                _ => null
            };

            if (condition is not null && CallsInterlockedExchange(condition, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CallsInterlockedExchange(
        ExpressionSyntax condition,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var call in condition.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(call, cancellationToken).Symbol is not IMethodSymbol method
                || !string.Equals(method.ContainingType?.ToDisplayString(), InterlockedTypeName, StringComparison.Ordinal)
                || (!string.Equals(method.Name, "Exchange", StringComparison.Ordinal)
                    && !string.Equals(method.Name, "CompareExchange", StringComparison.Ordinal)))
            {
                continue;
            }

            var arguments = call.ArgumentList.Arguments;

            if (arguments.Count > 0
                && arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                && model.GetSymbolInfo(arguments[0].Expression, cancellationToken).Symbol is IFieldSymbol)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsThrowingCompleter(string name)
    {
        return string.Equals(name, "SetResult", StringComparison.Ordinal)
               || string.Equals(name, "SetException", StringComparison.Ordinal)
               || string.Equals(name, "SetCanceled", StringComparison.Ordinal);
    }

    private static void AddDefinitions(Compilation compilation, string metadataName, List<INamedTypeSymbol> target)
    {
        target.AddRange(MetadataTypes.Find(compilation, metadataName));
    }

    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context, List<INamedTypeSymbol> sources)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;

        if (!IsCompletionSource(creation, sources, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (SuppressesTheReport(creation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation()));
    }

    private static bool IsCompletionSource(
        BaseObjectCreationExpressionSyntax creation,
        List<INamedTypeSymbol> sources,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return IsCompletionSourceType(
            model.GetTypeInfo(creation, cancellationToken).Type as INamedTypeSymbol,
            sources);
    }

    private static bool IsCompletionSourceType(INamedTypeSymbol? type, List<INamedTypeSymbol> sources)
    {
        if (type is null)
        {
            return false;
        }

        var definition = type.OriginalDefinition;

        foreach (var source in sources)
        {
            if (SymbolEqualityComparer.Default.Equals(source, definition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when the call already carries the flag, and true when the rule cannot decide. Only a
    ///     missing or zeroed flag returns false.
    /// </summary>
    private static bool SuppressesTheReport(
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var optionsIndex = GetOptionsParameterIndex(creation, model, cancellationToken);

        if (optionsIndex is not { } index || index < 0)
        {
            // No options parameter means the source always runs continuations inline.
            return false;
        }

        var arguments = creation.ArgumentList?.Arguments;

        if (arguments is null || index >= arguments.Value.Count)
        {
            return false;
        }

        var constant = model.GetConstantValue(arguments.Value[index].Expression, cancellationToken);

        if (!constant.HasValue || constant.Value is not int options)
        {
            // A computed value carries no decision, so the rule stays silent.
            return true;
        }

        return (options & RunContinuationsValue) == RunContinuationsValue;
    }

    /// <summary>
    ///     Returns the index of the <c lang="csharp">TaskCreationOptions</c> parameter, or -1 when the
    ///     overload takes none. Returns null when the bound symbol is unknown, which happens while the
    ///     buffer does not compile.
    /// </summary>
    internal static int? GetOptionsParameterIndex(
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(creation, cancellationToken).Symbol is not IMethodSymbol constructor)
        {
            return null;
        }

        for (var i = 0; i < constructor.Parameters.Length; i++)
        {
            if (FipsSymbolHelpers.IsType(
                    constructor.Parameters[i].Type,
                    OptionsTypeName,
                    "System.Threading.Tasks"))
            {
                return i;
            }
        }

        return -1;
    }
}
