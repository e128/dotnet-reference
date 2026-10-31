using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ToListInForeachAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128018";

    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use ToArray() instead of ToList() for read-only foreach iteration",
        "ToList() in foreach loop allocates an unnecessary list — use ToArray() for read-only iteration",
        "Performance",
        DiagnosticSeverity.Warning,
        true,
        "When a ToList() call is the direct expression of a foreach loop, the list is never mutated. Use ToArray() instead to avoid the per-element resizing overhead and signal read-only intent.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            // A type with its own ToList() is not the LINQ method, so the rule has nothing to say
            // about it. Report only when System.Linq is in scope.
            var enumerableType = startContext.Compilation.GetTypeByMetadataName(EnumerableMetadataName);
            if (enumerableType is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeForeach(nodeContext, enumerableType),
                SyntaxKind.ForEachStatement);
        });
    }

    private static void AnalyzeForeach(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType)
    {
        var forEach = (ForEachStatementSyntax)context.Node;

        if (forEach.Expression is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.Text, "ToList", StringComparison.Ordinal))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, enumerableType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }
}
