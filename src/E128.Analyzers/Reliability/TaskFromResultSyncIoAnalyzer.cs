using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
/// E128028: Flags methods that return <c>Task.FromResult</c> / <c>ValueTask.FromResult</c>
/// while also calling synchronous I/O methods that have async equivalents.
/// The fix is to convert the method to <c>async</c> and use the async I/O APIs.
/// </summary>
/// <remarks>
/// Legitimate patterns are NOT flagged:
/// - Early-return guards (parameter validation before I/O)
/// - Null-object implementations (no I/O at all)
/// - CPU-bound work (pure computation wrapped for interface compatibility)
/// - Sync-only library calls (no async alternative exists)
/// - Methods that already use <c>await</c>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskFromResultSyncIoAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128028";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Task.FromResult wraps sync I/O that has an async alternative",
        messageFormat: "Method '{0}' calls sync I/O '{1}' and wraps the result in {2} — use the async equivalent instead",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When a method returns Task.FromResult or ValueTask.FromResult but also calls " +
            "synchronous I/O methods that have async equivalents (e.g., File.ReadAllText instead of " +
            "File.ReadAllTextAsync), it blocks the calling thread unnecessarily. Convert to async/await.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>
    /// Maps sync I/O method names to their containing type (fully qualified metadata name).
    /// Includes methods with direct async equivalents (e.g., ReadAllText -> ReadAllTextAsync)
    /// and methods that should be offloaded via Task.Run (e.g., File.Copy, File.Move).
    /// </summary>
    internal static readonly ImmutableDictionary<string, string> SyncIoMethodsWithAsyncAlternatives =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // System.IO.File — methods with direct async alternatives
            ["ReadAllText"] = "System.IO.File",
            ["ReadAllBytes"] = "System.IO.File",
            ["ReadAllLines"] = "System.IO.File",
            ["ReadLines"] = "System.IO.File",
            ["WriteAllText"] = "System.IO.File",
            ["WriteAllBytes"] = "System.IO.File",
            ["WriteAllLines"] = "System.IO.File",
            ["AppendAllText"] = "System.IO.File",
            ["AppendAllLines"] = "System.IO.File",

            // System.IO.File — no direct async API, but should be offloaded via Task.Run
            ["Copy"] = "System.IO.File",
            ["Move"] = "System.IO.File",

            // System.IO.Stream
            ["Read"] = "System.IO.Stream",
            ["Write"] = "System.IO.Stream",
            ["CopyTo"] = "System.IO.Stream",
            ["Flush"] = "System.IO.Stream",

            // System.Net.Http.HttpClient
            ["GetStringAsync"] = "System.Net.Http.HttpClient",
            ["GetByteArrayAsync"] = "System.Net.Http.HttpClient",
            ["GetStreamAsync"] = "System.Net.Http.HttpClient",
            ["Send"] = "System.Net.Http.HttpClient",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var taskType = compilationContext.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var valueTaskType = compilationContext.Compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");

            if (taskType is null && valueTaskType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, taskType, valueTaskType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? taskType,
        INamedTypeSymbol? valueTaskType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsFromResultCall(invocation, context.SemanticModel, taskType, valueTaskType, context.CancellationToken, out var wrapperName))
        {
            return;
        }

        var containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (containingMethod is null)
        {
            return;
        }

        if (MethodContainsAwait(containingMethod))
        {
            return;
        }

        var syncIoCall = FindSyncIoCallWithAsyncAlternative(containingMethod, context.SemanticModel, context.CancellationToken);
        if (syncIoCall is null)
        {
            return;
        }

        var methodName = containingMethod.Identifier.ValueText;
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            methodName,
            syncIoCall,
            wrapperName));
    }

    private static bool IsFromResultCall(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol? taskType,
        INamedTypeSymbol? valueTaskType,
        CancellationToken cancellationToken,
        out string wrapperName)
    {
        wrapperName = string.Empty;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "FromResult", StringComparison.Ordinal))
        {
            return false;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return false;
        }

        var containingType = method.ContainingType?.OriginalDefinition;
        if (containingType is null)
        {
            return false;
        }

        if (taskType is not null && SymbolEqualityComparer.Default.Equals(containingType, taskType))
        {
            wrapperName = "Task.FromResult";
            return true;
        }

        if (valueTaskType is not null && SymbolEqualityComparer.Default.Equals(containingType, valueTaskType))
        {
            wrapperName = "ValueTask.FromResult";
            return true;
        }

        return false;
    }

    private static bool MethodContainsAwait(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes().Any(n => n.IsKind(SyntaxKind.AwaitExpression));
    }

    private static string? FindSyncIoCallWithAsyncAlternative(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var descendant in method.DescendantNodes())
        {
            if (descendant is not InvocationExpressionSyntax call)
            {
                continue;
            }

            if (call.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            var methodName = memberAccess.Name.Identifier.ValueText;

            if (!SyncIoMethodsWithAsyncAlternatives.TryGetValue(methodName, out var expectedContainingType))
            {
                continue;
            }

            var callSymbol = semanticModel.GetSymbolInfo(call, cancellationToken);
            if (callSymbol.Symbol is not IMethodSymbol calledMethod)
            {
                continue;
            }

            var containingTypeName = calledMethod.ContainingType?.ToDisplayString();
            if (string.Equals(containingTypeName, expectedContainingType, StringComparison.Ordinal))
            {
                return $"{expectedContainingType}.{methodName}";
            }
        }

        return null;
    }
}
