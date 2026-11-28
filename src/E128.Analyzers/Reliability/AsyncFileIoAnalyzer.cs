using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128092: Detects a synchronous <c lang="csharp">System.IO.File</c> call (<c lang="csharp">ReadAllText</c>,
///     <c lang="csharp">WriteAllBytes</c>, etc.) with an <c lang="csharp">Async</c> sibling, fired whenever the containing
///     method/local function/lambda is NOT already <c lang="csharp">async</c>. Complements CA1849/VSTHRD103,
///     which only fire inside an already-async method -- a wholly synchronous method gets no
///     signal from those rules at all.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncFileIoAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128092";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use the async File.*Async overload",
        "'{0}' has an async sibling '{1}' -- use it and make the containing method async instead of blocking synchronously",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "System.IO.File synchronous methods block the calling thread. CA1849/VSTHRD103 only flag this inside an already-async method; this rule also flags it in a fully synchronous method, where those analyzers give no signal at all.");

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
            static method => method.ContainingType is { } containingType
                             && TypeSymbols.IsNamed(containingType, "File", "System.IO")
                ? containingType
                : null,
            static (type, name) => type.GetMembers(name).OfType<IMethodSymbol>().Any());
    }
}
