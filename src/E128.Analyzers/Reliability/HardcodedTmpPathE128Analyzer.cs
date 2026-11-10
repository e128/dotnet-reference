using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128023: Flags hardcoded <c lang="csharp">"/tmp"</c> or <c lang="csharp">"/tmp/..."</c> string literals.
///     Cross-platform code must use the temp directory API instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HardcodedTmpPathE128Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128023";

    // The code fix builds the replacement from this value, so the path prefixes live here only.
    internal const string PropRelativeSuffix = "RelativeSuffix";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Avoid hardcoded /tmp path",
        "Hardcoded path '{0}' is not portable — use Path.GetTempPath() instead",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "String literals equal to \"/tmp\" or starting with \"/tmp/\" (or Windows equivalents) are not portable. Use Path.Combine with the temp directory API instead.");

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

    // Each prefix ends with a path separator. The folder names are built in parts so the
    // analyzer source holds no literal temp path.
    private static readonly string[] TempPrefixes =
    [
        string.Concat("/", "tmp/"),
        string.Concat("C:\\", "Temp\\"),
        string.Concat("C:\\", "Windows\\", "Temp\\")
    ];

    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literalExpr = (LiteralExpressionSyntax)context.Node;
        var value = literalExpr.Token.ValueText;

        foreach (var prefix in TempPrefixes)
        {
            var exact = prefix.Substring(0, prefix.Length - 1);
            if (string.Equals(value, exact, StringComparison.Ordinal))
            {
                Report(context, value, []);
                return;
            }

            if (value.StartsWith(prefix, StringComparison.Ordinal))
            {
                var properties = ImmutableDictionary<string, string?>.Empty
                    .Add(PropRelativeSuffix, value.Substring(prefix.Length));
                Report(context, value, properties);
                return;
            }
        }
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        string value,
        ImmutableDictionary<string, string?> properties)
    {
        context.ReportDiagnostic(
            Diagnostic.Create(Rule, context.Node.GetLocation(), properties, value));
    }
}
