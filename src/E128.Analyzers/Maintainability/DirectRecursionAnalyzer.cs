using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Maintainability;

/// <summary>
///     E128199: Reports a method that calls itself directly. The call graph becomes cyclic, so the
///     stack depth grows with the input. An explicit stack or a loop keeps the cost bounded.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DirectRecursionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128199";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Method calls itself directly",
        "Method '{0}' calls itself. Replace the recursion with iteration.",
        "E128-Maintainability",
        DiagnosticSeverity.Info,
        true,
        "A direct self-call makes the call graph cyclic, so the stack depth grows with the input. " +
        "An explicit stack or a loop keeps the cost bounded and the control flow provable.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationBlockAction(AnalyzeBlock);
    }

    private static void AnalyzeBlock(OperationBlockAnalysisContext context)
    {
        if (context.OwningSymbol is not IMethodSymbol method)
        {
            return;
        }

        foreach (var block in context.OperationBlocks)
        {
            if (!CallsItself(block, method))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, method.Locations.Length > 0 ? method.Locations[0] : Location.None, method.Name));
            return;
        }
    }

    private static bool CallsItself(IOperation block, IMethodSymbol method)
    {
        foreach (var operation in block.DescendantsAndSelf())
        {
            if (operation is IInvocationOperation invocation
                && !IsInsideAnonymousFunction(operation)
                && IsSelfReceiver(invocation)
                && SymbolEqualityComparer.Default.Equals(
                    invocation.TargetMethod.OriginalDefinition,
                    method.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    // A call through another instance is not direct recursion on this frame, even when the type
    // matches. An implicit or explicit `this` receiver is.
    private static bool IsSelfReceiver(IInvocationOperation invocation)
    {
        return invocation.TargetMethod.IsStatic
            || invocation.Instance is null
            || invocation.Instance is IInstanceReferenceOperation;
    }

    // A call inside a lambda runs when the lambda runs, not inside the method frame that declares
    // it. A deferred callback such as "queue.Post(() => Start())" is not direct recursion.
    private static bool IsInsideAnonymousFunction(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation)
            {
                return true;
            }
        }

        return false;
    }
}
