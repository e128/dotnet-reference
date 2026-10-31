using System;
using System.Collections.Concurrent;
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
///     E128028: Flags methods that return <c lang="csharp">Task.FromResult</c> / <c lang="csharp">ValueTask.FromResult</c>
///     while also calling synchronous I/O methods that have async equivalents.
///     The fix is to convert the method to <c lang="csharp">async</c> and use the async I/O APIs.
/// </summary>
/// <remarks>
///     Legitimate patterns are NOT flagged:
///     - Early-return guards (parameter validation before I/O)
///     - Null-object implementations (no I/O at all)
///     - CPU-bound work (pure computation wrapped for interface compatibility)
///     - Sync-only library calls (no async alternative exists)
///     - Methods that already use <c lang="csharp">await</c>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskFromResultSyncIoAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128028";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Task.FromResult wraps sync I/O that has an async alternative",
        "Method '{0}' calls sync I/O '{1}' and wraps the result in {2} — use the async equivalent instead",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "When a method returns Task.FromResult or ValueTask.FromResult but also calls " +
        "synchronous I/O methods that have async equivalents (e.g., File.ReadAllText instead of " +
        "File.ReadAllTextAsync), it blocks the calling thread unnecessarily. Convert to async/await.");

    /// <summary>
    ///     Maps sync I/O method names to their containing type (fully qualified metadata name).
    ///     Holds only methods that have a direct awaitable equivalent, so the code fix can always
    ///     replace the call. File.Copy, File.Move, and File.ReadLines are absent because no async
    ///     overload awaits cleanly.
    /// </summary>
    internal static readonly ImmutableDictionary<string, string> SyncIoMethodsWithAsyncAlternatives =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // System.IO.File
            ["ReadAllText"] = "System.IO.File",
            ["ReadAllBytes"] = "System.IO.File",
            ["ReadAllLines"] = "System.IO.File",
            ["WriteAllText"] = "System.IO.File",
            ["WriteAllBytes"] = "System.IO.File",
            ["WriteAllLines"] = "System.IO.File",
            ["AppendAllText"] = "System.IO.File",
            ["AppendAllLines"] = "System.IO.File",

            // System.IO.Stream
            ["Read"] = "System.IO.Stream",
            ["Write"] = "System.IO.Stream",
            ["CopyTo"] = "System.IO.Stream",
            ["Flush"] = "System.IO.Stream",

            // System.Net.Http.HttpClient — sync overload only
            ["Send"] = "System.Net.Http.HttpClient"
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     Maps each key of <see cref="SyncIoMethodsWithAsyncAlternatives" /> to its async name.
    ///     The two tables are one source of truth and must keep the same keys.
    /// </summary>
    internal static readonly ImmutableDictionary<string, string> SyncToAsyncMethodNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ReadAllText"] = "ReadAllTextAsync",
            ["ReadAllBytes"] = "ReadAllBytesAsync",
            ["ReadAllLines"] = "ReadAllLinesAsync",
            ["WriteAllText"] = "WriteAllTextAsync",
            ["WriteAllBytes"] = "WriteAllBytesAsync",
            ["WriteAllLines"] = "WriteAllLinesAsync",
            ["AppendAllText"] = "AppendAllTextAsync",
            ["AppendAllLines"] = "AppendAllLinesAsync",
            ["Read"] = "ReadAsync",
            ["Write"] = "WriteAsync",
            ["CopyTo"] = "CopyToAsync",
            ["Flush"] = "FlushAsync",
            ["Send"] = "SendAsync"
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

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

            var availableSyncIoMethods = FindAvailableSyncIoMethods(compilationContext.Compilation);
            if (availableSyncIoMethods.IsEmpty)
            {
                return;
            }

            // The await and sync-I/O scans depend only on the enclosing method, not on the
            // individual FromResult call. Cache the result per method.
            var methodScans =
                new ConcurrentDictionary<MethodDeclarationSyntax, (bool HasAwait, string? SyncIoCall)>();

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, taskType, valueTaskType, availableSyncIoMethods, methodScans),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>
    ///     Keeps the pairs whose async half exists in the compilation. The async file APIs arrived
    ///     after the sync ones, so a netstandard2.0 target has no <c lang="csharp">File.ReadAllTextAsync</c>.
    ///     Reporting a method there would give a fix that does not compile.
    /// </summary>
    internal static ImmutableDictionary<string, string> FindAvailableSyncIoMethods(Compilation compilation)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        foreach (var entry in SyncIoMethodsWithAsyncAlternatives)
        {
            var owner = compilation.GetTypeByMetadataName(entry.Value);
            if (owner is null
                || !owner.GetMembers(SyncToAsyncMethodNames[entry.Key]).OfType<IMethodSymbol>().Any())
            {
                continue;
            }

            builder.Add(entry.Key, entry.Value);
        }

        return builder.ToImmutable();
    }

    /// <summary>
    ///     Returns true when the node sits inside a lambda or a local function nested in
    ///     <paramref name="method" />. Neither can take an await unless it becomes async itself,
    ///     so neither belongs to the body of the method under analysis.
    /// </summary>
    internal static bool IsInNestedFunction(SyntaxNode node, MethodDeclarationSyntax method)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor == method)
            {
                return false;
            }

            if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? taskType,
        INamedTypeSymbol? valueTaskType,
        ImmutableDictionary<string, string> availableSyncIoMethods,
        ConcurrentDictionary<MethodDeclarationSyntax, (bool HasAwait, string? SyncIoCall)> methodScans)
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

        var semanticModel = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        var (hasAwait, syncIoCall) = methodScans.GetOrAdd(containingMethod, method =>
        {
            if (MethodContainsAwait(method))
            {
                return (true, null);
            }

            return (false, FindSyncIoCallWithAsyncAlternative(method, semanticModel, availableSyncIoMethods, cancellationToken));
        });

        if (hasAwait || syncIoCall is not { } syncCall)
        {
            return;
        }

        var methodName = containingMethod.Identifier.ValueText;
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            methodName,
            syncCall,
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
        return method.DescendantNodes()
            .Any(node => node.IsKind(SyntaxKind.AwaitExpression) && !IsInNestedFunction(node, method));
    }

    private static string? FindSyncIoCallWithAsyncAlternative(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        ImmutableDictionary<string, string> availableSyncIoMethods,
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

            if (!availableSyncIoMethods.TryGetValue(methodName, out var expectedContainingType))
            {
                continue;
            }

            if (IsInNestedFunction(call, method))
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
