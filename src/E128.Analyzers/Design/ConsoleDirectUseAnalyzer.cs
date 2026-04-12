using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
/// E128045: Flags direct usage of <c>System.Console</c> members.
/// Use <c>ILogger</c> (services) or <c>ITerminalWriter</c>/<c>ITerminalPrompt</c> (CLI) instead.
/// </summary>
/// <remarks>
/// No code fix is provided — the replacement depends on whether the consuming code
/// is a service (use <c>ILogger</c>) or a CLI tool (use <c>ITerminalPrompt</c>).
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConsoleDirectUseAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128045";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Avoid direct System.Console usage",
        messageFormat: "Use ILogger (services) or ITerminalWriter/ITerminalPrompt (CLI) instead of System.Console.{0}",
        category: "Design",
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
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        var symbol = symbolInfo.Symbol;
        if (symbol is null)
        {
            return;
        }

        var containingType = symbol.ContainingType;
        if (containingType is null)
        {
            return;
        }

        if (!IsSystemConsole(containingType))
        {
            return;
        }

        var memberName = memberAccess.Name.Identifier.ValueText;
        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation(), memberName));
    }

    private static bool IsSystemConsole(INamedTypeSymbol type) =>
        string.Equals(type.Name, "Console", StringComparison.Ordinal)
        && type.ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };
}
