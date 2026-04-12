using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InCancellationTokenE128Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128019";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Do not pass CancellationToken by 'in' reference",
        messageFormat: "Parameter '{0}' uses 'in CancellationToken' — remove the 'in' modifier",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The 'in' modifier makes CancellationToken a by-ref parameter (CancellationToken&). " +
            "Reflection-based frameworks such as Microsoft.Extensions.AI cannot serialize by-ref " +
            "parameters, causing runtime failures. CancellationToken is a small struct; passing " +
            "it by value has no measurable overhead.");

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

        if (!IsCancellationToken(namedType))
        {
            return;
        }

        var name = parameter.Identifier.ValueText;
        context.ReportDiagnostic(Diagnostic.Create(Rule, parameter.GetLocation(), name));
    }

    private static bool IsCancellationToken(INamedTypeSymbol type) =>
        string.Equals(type.Name, "CancellationToken", StringComparison.Ordinal)
        && type.ContainingNamespace is { Name: "Threading" }
            and { ContainingNamespace: { Name: "System" } and { ContainingNamespace.IsGlobalNamespace: true } };
}
