using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
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
///     call stays silent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskCompletionSourceOptionsAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128159";

    internal const string OptionsTypeName = "TaskCreationOptions";

    internal const string RunContinuationsFlag = "TaskCreationOptions.RunContinuationsAsynchronously";

    internal const int RunContinuationsValue = 0x40;

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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

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
        if (model.GetTypeInfo(creation, cancellationToken).Type is not INamedTypeSymbol created)
        {
            return false;
        }

        var definition = created.OriginalDefinition;

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
            if (string.Equals(constructor.Parameters[i].Type.Name, OptionsTypeName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
