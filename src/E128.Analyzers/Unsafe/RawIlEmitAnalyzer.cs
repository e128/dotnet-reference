using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     E128138: Code emits raw IL through <c lang="csharp">ILGenerator</c>. Raw IL has no type
///     check, no memory check, and no trim or AOT guarantee.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RawIlEmitAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128138";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not emit raw IL with ILGenerator",
        "'{0}' emits raw IL. Use a source generator, or [UnsafeAccessor] for a private member.",
        "Unsafe",
        DiagnosticSeverity.Warning,
        true,
        "Raw IL bypasses the type system and the memory safety checks. A source generator is " +
        "visible to the compiler and to the trimmer. [UnsafeAccessor] reaches a private member " +
        "with a supported mechanism.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var ilGenerator = start.Compilation.GetTypeByMetadataName("System.Reflection.Emit.ILGenerator");
            if (ilGenerator is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, ilGenerator),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol ilGenerator)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, ilGenerator))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), method.Name));
    }
}
