using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128093: Detects a synchronous <c lang="csharp">DbConnection</c>/<c lang="csharp">DbCommand</c> call (<c lang="csharp">Open</c>,
///     <c lang="csharp">ExecuteReader</c>, <c lang="csharp">ExecuteNonQuery</c>, <c lang="csharp">ExecuteScalar</c>, etc. -- including on
///     provider subclasses such as <c lang="csharp">SqliteConnection</c>/<c lang="csharp">SqliteCommand</c>) with an
///     <c lang="csharp">Async</c> sibling, fired whenever the containing method/local function/lambda is NOT
///     already <c lang="csharp">async</c>. Same shape as E128092 for <c lang="csharp">System.IO.File</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncDbIoAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128093";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use the async DbConnection/DbCommand *Async overload",
        "'{0}' has an async sibling '{1}' -- use it and make the containing method async instead of blocking synchronously",
        "E128-Reliability",
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
        AsyncSiblingRules.Analyze(
            context,
            Rule,
            static method => method.ReceiverType is { } receiverType && IsDbConnectionOrCommand(receiverType)
                ? receiverType
                : null,
            HasAsyncSibling);
    }

    private static bool IsDbConnectionOrCommand(ITypeSymbol type)
    {
        return TypeSymbols.WalkBaseTypes(
            type,
            static current => TypeSymbols.IsNamed(current, "DbConnection", "System.Data.Common")
                              || TypeSymbols.IsNamed(current, "DbCommand", "System.Data.Common"));
    }

    private static bool HasAsyncSibling(ITypeSymbol type, string asyncSiblingName)
    {
        return TypeSymbols.WalkBaseTypes(
            type,
            current => current.GetMembers(asyncSiblingName).OfType<IMethodSymbol>().Any());
    }
}
