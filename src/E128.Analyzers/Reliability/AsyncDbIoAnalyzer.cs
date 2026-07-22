using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128093: Detects a synchronous <c>DbConnection</c>/<c>DbCommand</c> call (<c>Open</c>,
///     <c>ExecuteReader</c>, <c>ExecuteNonQuery</c>, <c>ExecuteScalar</c>, etc. -- including on
///     provider subclasses such as <c>SqliteConnection</c>/<c>SqliteCommand</c>) with an
///     <c>Async</c> sibling, fired whenever the containing method/local function/lambda is NOT
///     already <c>async</c>. Same shape as E128092 for <c>System.IO.File</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncDbIoAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128093";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use the async DbConnection/DbCommand *Async overload",
        "'{0}' has an async sibling '{1}' -- use it and make the containing method async instead of blocking synchronously",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "DbConnection/DbCommand synchronous methods (Open, ExecuteReader, ExecuteNonQuery, ExecuteScalar, ...) block the calling thread even on provider subclasses like SqliteConnection/SqliteCommand. CA1849/VSTHRD103 only flag this inside an already-async method; this rule also flags it in a fully synchronous method, where those analyzers give no signal at all.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.Name.EndsWith("Async", StringComparison.Ordinal))
        {
            return;
        }

        if (method.ReceiverType is not { } receiverType || !IsDbConnectionOrCommand(receiverType))
        {
            return;
        }

        var asyncSiblingName = method.Name + "Async";

        // Existence check only -- no parameter-shape comparison, same rationale as E128092.
        // The sibling can live on the receiver type itself or any DbConnection/DbCommand base
        // (e.g. Microsoft.Data.Sqlite's SqliteCommand overrides ExecuteReader() but inherits
        // ExecuteReaderAsync from DbCommand without overriding it).
        if (!HasAsyncSibling(receiverType, asyncSiblingName))
        {
            return;
        }

        if (AsyncContextSyntaxHelper.IsInsideAsyncContext(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), method.Name, asyncSiblingName));
    }

    private static bool IsDbConnectionOrCommand(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name is "DbConnection" or "DbCommand"
                && string.Equals(current.ContainingNamespace?.ToDisplayString(), "System.Data.Common", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAsyncSibling(ITypeSymbol type, string asyncSiblingName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(asyncSiblingName).OfType<IMethodSymbol>().Any())
            {
                return true;
            }
        }

        return false;
    }
}
