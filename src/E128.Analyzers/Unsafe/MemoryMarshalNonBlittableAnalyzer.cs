using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemoryMarshalNonBlittableAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128118";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not reinterpret bytes as a non-blittable type",
        "MemoryMarshal.{0} treats the type as a raw bit pattern. {1} is not blittable, so the result is not portable.",
        "E128-Unsafe",
        DiagnosticSeverity.Warning,
        true);

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
            var memoryMarshal = MetadataTypes.Find(start.Compilation, "System.Runtime.InteropServices.MemoryMarshal");
            if (memoryMarshal.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, memoryMarshal),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> memoryMarshal)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (InvocationNames.GetSimpleName(invocation) is not { } memberAccess)
        {
            return;
        }

        var name = memberAccess.Identifier.ValueText;
        if (!string.Equals(name, "Read", StringComparison.Ordinal)
            && !string.Equals(name, "Cast", StringComparison.Ordinal))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !MetadataTypes.Contains(memoryMarshal, method.ContainingType))
        {
            return;
        }

        var target = GetInterpretedType(method, name);
        if (target is null || Blittability.IsBlittable(target))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), name, target.Name));
    }

    private static ITypeSymbol? GetInterpretedType(IMethodSymbol method, string name)
    {
        var index = string.Equals(name, "Read", StringComparison.Ordinal) ? 0 : 1;

        return method.TypeArguments.Length == index + 1 ? method.TypeArguments[index] : null;
    }

}
