using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128021: Flags <see langword="in" /> on ref struct parameters (<see cref="Span{T}" />,
///     <see cref="ReadOnlySpan{T}" />, etc.). Ref structs are already passed by
///     reference — <see langword="in" /> is redundant at best and a compile error on
///     extension method <see langword="this" /> parameters.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InRefStructE128Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128021";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not use 'in' modifier with ref struct parameters",
        "Parameter '{0}' uses 'in {1}' — ref structs are already passed by reference; remove the 'in' modifier",
        "Design",
        DiagnosticSeverity.Error,
        true,
        "Ref structs (Span<T>, ReadOnlySpan<T>, etc.) are always passed by reference on the stack. " +
        "Adding 'in' is redundant and misleading. On extension method 'this' parameters it is a compile error.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        var parameter = (ParameterSyntax)context.Node;

        if (!InModifierHelper.HasInModifier(parameter))
        {
            return;
        }

        if (parameter.Type is null)
        {
            return;
        }

        var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type, context.CancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol namedType)
        {
            return;
        }

        if (!namedType.IsRefLikeType)
        {
            return;
        }

        var name = parameter.Identifier.ValueText;
        var typeName = namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(Rule, parameter.GetLocation(), name, typeName));
    }
}
