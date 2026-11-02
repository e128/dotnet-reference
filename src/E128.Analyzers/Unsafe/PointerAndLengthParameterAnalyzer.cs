using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     E128136: A method takes a pointer and a length. The pair carries no bounds check, and the
///     caller must keep the buffer alive. <c lang="csharp">Span&lt;T&gt;</c> carries the length.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PointerAndLengthParameterAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128136";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use Span<T> instead of a pointer and a length",
        "'{0}' takes a pointer and a length. Pass Span<T> or ReadOnlySpan<T> instead.",
        "Unsafe",
        DiagnosticSeverity.Warning,
        true,
        "A pointer parameter paired with a length parameter has no bounds check and no lifetime " +
        "contract. Span<T> and ReadOnlySpan<T> carry both, and the JIT removes the bounds check " +
        "when the access is provably in range.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken)
            is not IMethodSymbol method)
        {
            return;
        }

        var hasLength = false;
        var hasPointer = false;

        foreach (var parameter in method.Parameters)
        {
            hasLength |= IsLengthType(parameter.Type);
            hasPointer |= parameter.Type is IPointerTypeSymbol;
        }

        if (!hasLength || !hasPointer)
        {
            return;
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type is IPointerTypeSymbol)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, parameter.Locations[0], method.Name));
            }
        }
    }

    private static bool IsLengthType(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_IntPtr
            or SpecialType.System_UIntPtr;
    }
}
