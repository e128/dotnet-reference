using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
/// E128023: Flags hardcoded <c>"/tmp"</c> or <c>"/tmp/..."</c> string literals.
/// Cross-platform code must use the temp directory API instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HardcodedTmpPathE128Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128023";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Avoid hardcoded /tmp path",
        messageFormat: "Hardcoded path '{0}' is not portable — use Path.GetTempPath() instead",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "String literals equal to \"/tmp\" or starting with \"/tmp/\" (or Windows equivalents) are not portable. Use Path.Combine with the temp directory API instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
    }

    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literalExpr = (LiteralExpressionSyntax)context.Node;
        var value = literalExpr.Token.ValueText;

        if (string.Equals(value, "/tmp", StringComparison.Ordinal)
            || value.StartsWith("/tmp/", StringComparison.Ordinal)
            || string.Equals(value, @"C:\Temp", StringComparison.Ordinal)
            || value.StartsWith(@"C:\Temp\", StringComparison.Ordinal)
            || string.Equals(value, @"C:\Windows\Temp", StringComparison.Ordinal)
            || value.StartsWith(@"C:\Windows\Temp\", StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), value));
        }
    }
}
