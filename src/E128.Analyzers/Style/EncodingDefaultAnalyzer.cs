using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EncodingDefaultAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128006";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use Encoding.UTF8 instead of Encoding.Default",
        "Use Encoding.UTF8 instead of Encoding.Default — Encoding.Default is platform-specific",
        "Style",
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
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Default", StringComparison.Ordinal))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol property)
        {
            return;
        }

        var containingType = property.ContainingType;
        if (containingType is null || !IsSystemTextEncoding(containingType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation()));
    }

    private static bool IsSystemTextEncoding(INamedTypeSymbol type)
    {
        return string.Equals(type.Name, "Encoding", StringComparison.Ordinal)
               && type.ContainingNamespace is { Name: "Text" }
               && type.ContainingNamespace.ContainingNamespace is { Name: "System" };
    }
}
