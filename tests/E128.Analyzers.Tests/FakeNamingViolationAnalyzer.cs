using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Tests;

/// <summary>
/// Fake analyzer that emits IDE1006 with naming style properties on private fields
/// that don't already start with '_'. Used only in tests.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class FakeNamingViolationAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "IDE1006",
        title: "Naming rule violation",
        messageFormat: "Naming rule violation: Missing prefix: '_'",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;

        var hasPrivate = false;
        foreach (var modifier in field.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PrivateKeyword))
            {
                hasPrivate = true;
                break;
            }
        }

        if (!hasPrivate)
        {
            return;
        }

        foreach (var variable in field.Declaration.Variables)
        {
            var name = variable.Identifier.ValueText;
            if (name.StartsWith('_'))
            {
                continue;
            }

            var properties = ImmutableDictionary.CreateRange<string, string?>(
            [
                new("SymbolName", name),
                new("Prefix", "_"),
                new("Suffix", string.Empty),
                new("WordSeparator", string.Empty),
                new("CapitalizationScheme", "CamelCase"),
            ]);

            var diagnostic = Diagnostic.Create(Rule, variable.Identifier.GetLocation(), properties);
            context.ReportDiagnostic(diagnostic);
        }
    }
}