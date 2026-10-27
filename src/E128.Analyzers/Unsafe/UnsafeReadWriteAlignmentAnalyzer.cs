using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsafeReadWriteAlignmentAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128113";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use the unaligned Unsafe read and write methods",
        "Unsafe.{0} assumes aligned memory. Use Unsafe.{0}Unaligned unless the address is provably aligned.",
        "Unsafe",
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
            var unsafeType = MetadataTypes.Find(start.Compilation, "System.Runtime.CompilerServices.Unsafe");
            if (unsafeType.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, unsafeType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> unsafeType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (InvocationNames.GetSimpleName(invocation) is not { } memberAccess)
        {
            return;
        }

        var name = memberAccess.Identifier.ValueText;
        if (!IsUnalignedVariant(name, out _))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !MetadataTypes.Contains(unsafeType, method.ContainingType)
            || method.TypeArguments.Length != 1)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), name));
    }

    internal static bool IsUnalignedVariant(string name, out string replacement)
    {
        if (string.Equals(name, "Read", StringComparison.Ordinal))
        {
            replacement = "ReadUnaligned";
            return true;
        }

        if (string.Equals(name, "Write", StringComparison.Ordinal))
        {
            replacement = "WriteUnaligned";
            return true;
        }

        replacement = string.Empty;
        return false;
    }
}
