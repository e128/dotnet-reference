using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     E128139: <c lang="csharp">Unsafe.ReadUnaligned</c> and <c lang="csharp">Unsafe.WriteUnaligned</c>
///     treat the memory as a raw bit pattern. A type that is not blittable has no stable bit pattern,
///     so the result is not portable.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsafeUnalignedNonBlittableAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128139";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not move a non-blittable type through an unaligned access",
        "Unsafe.{0}<{1}> treats the memory as a raw bit pattern. {1} is not blittable, so the result is not portable.",
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
        if (!string.Equals(name, "ReadUnaligned", StringComparison.Ordinal)
            && !string.Equals(name, "WriteUnaligned", StringComparison.Ordinal))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !MetadataTypes.Contains(unsafeType, method.ContainingType)
            || method.TypeArguments.Length != 1)
        {
            return;
        }

        var target = method.TypeArguments[0];
        if (target.TypeKind == TypeKind.TypeParameter || MemoryMarshalNonBlittableAnalyzer.IsBlittable(target))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            name,
            target.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }
}
