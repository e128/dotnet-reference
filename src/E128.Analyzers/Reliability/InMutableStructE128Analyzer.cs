using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InMutableStructE128Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128020";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not use 'in' modifier with mutable structs",
        "Parameter '{0}' should not use the 'in' modifier — '{1}' is a mutable struct and 'in' causes hidden defensive copies",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "The 'in' modifier on a mutable (non-readonly) struct parameter causes the compiler to " +
        "create a hidden defensive copy on every member access. This silently changes behavior — " +
        "for example, enumerating a copy of Batch<Activity> yields zero elements.");

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

        if (!IsMutableStruct(namedType))
        {
            return;
        }

        var name = parameter.Identifier.ValueText;
        var typeName = namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(Rule, parameter.GetLocation(), name, typeName));
    }

    private static bool IsMutableStruct(INamedTypeSymbol type)
    {
        return type.IsValueType && !type.IsReadOnly && !type.IsRefLikeType && type.SpecialType == SpecialType.None && type.TypeKind != TypeKind.Enum;
    }
}
