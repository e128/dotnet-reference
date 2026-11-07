using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128181: Flags an <c lang="csharp">HttpClient.Timeout</c> set to an infinite value.
///     The request then has no deadline, so a stalled server hangs the caller and its
///     thread pool slot with no recovery path.
/// </summary>
/// <remarks>
///     The rule reports the named infinite forms only: <c lang="csharp">Timeout.InfiniteTimeSpan</c>,
///     <c lang="csharp">TimeSpan.FromMilliseconds(-1)</c>, and
///     <c lang="csharp">TimeSpan.FromMilliseconds(Timeout.Infinite)</c>. A computed value stays
///     silent because the rule cannot prove it is infinite.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpClientInfiniteTimeoutAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128181";

    private const string HttpClientMetadataName = "System.Net.Http.HttpClient";

    private const string TimeoutMetadataName = "System.Threading.Timeout";

    private const string TimeSpanMetadataName = "System.TimeSpan";

    private const string TimeoutPropertyName = "Timeout";

    private const string InfiniteTimeSpanFieldName = "InfiniteTimeSpan";

    private const string FromMillisecondsMethodName = "FromMilliseconds";

    private const int InfiniteMilliseconds = -1;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "HttpClient timeout is infinite",
        "HttpClient.Timeout is set to an infinite value, so a stalled server never times out and the caller hangs",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "HttpClient.Timeout defaults to 100 seconds. Setting it to Timeout.InfiniteTimeSpan, " +
        "TimeSpan.FromMilliseconds(-1), or TimeSpan.FromMilliseconds(Timeout.Infinite) " +
        "removes the request deadline. A stalled server then holds the caller and its thread pool slot " +
        "with no recovery.");

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
        var httpClientTypes = MetadataTypes.Find(context.Compilation, HttpClientMetadataName);
        var timeoutTypes = MetadataTypes.Find(context.Compilation, TimeoutMetadataName);
        var timeSpanTypes = MetadataTypes.Find(context.Compilation, TimeSpanMetadataName);

        if (httpClientTypes.IsEmpty || timeoutTypes.IsEmpty || timeSpanTypes.IsEmpty)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeAssignment(nodeContext, httpClientTypes, timeoutTypes, timeSpanTypes),
            SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeAssignment(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> httpClientTypes,
        ImmutableArray<INamedTypeSymbol> timeoutTypes,
        ImmutableArray<INamedTypeSymbol> timeSpanTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
                is not IPropertySymbol property
            || !string.Equals(property.Name, TimeoutPropertyName, StringComparison.Ordinal)
            || !MetadataTypes.Contains(httpClientTypes, property.ContainingType))
        {
            return;
        }

        if (!IsInfiniteTimeout(
                assignment.Right,
                context.SemanticModel,
                timeoutTypes,
                timeSpanTypes,
                context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation()));
    }

    // A computed value cannot be proven infinite, so the rule reads the constant value through
    // the semantic model instead of the source text. The framework's infinite marker is -1 ms.
    // Any other value, including a short timeout below 100 ms, keeps a finite deadline.
    private static bool IsInfiniteTimeout(
        ExpressionSyntax value,
        SemanticModel model,
        ImmutableArray<INamedTypeSymbol> timeoutTypes,
        ImmutableArray<INamedTypeSymbol> timeSpanTypes,
        CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(value, cancellationToken).Symbol;

        if (symbol is IFieldSymbol field)
        {
            return IsInfiniteTimeSpanField(field, timeoutTypes);
        }

        if (symbol is not IMethodSymbol method
            || !string.Equals(method.Name, FromMillisecondsMethodName, StringComparison.Ordinal)
            || !MetadataTypes.Contains(timeSpanTypes, method.ContainingType)
            || value is not InvocationExpressionSyntax invocation
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        var argument = invocation.ArgumentList.Arguments[0].Expression;
        return model.GetConstantValue(argument, cancellationToken) is { HasValue: true, Value: var constantValue }
            && IsInfiniteMilliseconds(constantValue);
    }

    private static bool IsInfiniteTimeSpanField(
        IFieldSymbol field,
        ImmutableArray<INamedTypeSymbol> timeoutTypes)
    {
        return string.Equals(field.Name, InfiniteTimeSpanFieldName, StringComparison.Ordinal)
            && MetadataTypes.Contains(timeoutTypes, field.ContainingType);
    }

    private static bool IsInfiniteMilliseconds(object? value)
    {
        return (value is int intValue && intValue == InfiniteMilliseconds)
            || (value is long longValue && longValue == InfiniteMilliseconds)
            || (value is double doubleValue && IsInfiniteDouble(doubleValue));
    }

    // The marker is exactly -1 ms, and the framework converts the argument without rounding.
    private static bool IsInfiniteDouble(double value)
    {
        return Math.Abs(value - InfiniteMilliseconds) < double.Epsilon;
    }
}
