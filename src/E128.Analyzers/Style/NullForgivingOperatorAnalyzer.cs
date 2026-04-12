using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
/// E128043: Flags every use of the null-forgiving operator (<c>!</c>).
/// The operator suppresses nullable analysis warnings without actually ensuring
/// the value is non-null, hiding potential NullReferenceExceptions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullForgivingOperatorAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128043";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Do not use the null-forgiving operator",
        messageFormat: "Replace null-forgiving operator '!' with a proper null check or type annotation",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.SuppressNullableWarningExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation()));
    }
}
