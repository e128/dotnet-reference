using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RuntimeRegexToGeneratedAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128123";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use GeneratedRegex for a constant pattern",
        "This Regex is built at run time. [GeneratedRegex] builds the matcher at compile time and keeps the pattern in source.",
        "Performance",
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
        context.RegisterCompilationStartAction(start =>
        {
            var regex = MetadataTypes.Find(start.Compilation, "System.Text.RegularExpressions.Regex");

            // Without GeneratedRegexAttribute (before .NET 7) the rule has no remedy.
            if (regex.IsEmpty
                || MetadataTypes.Find(start.Compilation, "System.Text.RegularExpressions.GeneratedRegexAttribute").IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(node => AnalyzeField(node, regex), SyntaxKind.FieldDeclaration);
        });
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> regex)
    {
        var field = (FieldDeclarationSyntax)context.Node;

        // The fix emits a partial property, which needs C# 13.
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions parseOptions
            || parseOptions.LanguageVersion.MapSpecifiedToEffectiveVersion() < LanguageVersion.CSharp13)
        {
            return;
        }

        if (!HasModifier(field, SyntaxKind.StaticKeyword)
            || !HasModifier(field, SyntaxKind.ReadOnlyKeyword)
            || field.Declaration.Variables.Count != 1
            || field.Parent is not TypeDeclarationSyntax)
        {
            return;
        }

        if (!MetadataTypes.Contains(
                regex,
                context.SemanticModel.GetTypeInfo(field.Declaration.Type, context.CancellationToken).Type))
        {
            return;
        }

        if (TryGetCreation(field.Declaration.Variables[0]) is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, field.GetLocation()));
    }

    internal static ObjectCreationExpressionSyntax? TryGetCreation(VariableDeclaratorSyntax variable)
    {
        var creation = variable.Initializer?.Value as ObjectCreationExpressionSyntax;

        return creation is not null && HasConstantPattern(creation) ? creation : null;
    }

    private static bool HasConstantPattern(ObjectCreationExpressionSyntax creation)
    {
        var argumentList = creation.ArgumentList;

        return argumentList is not null
            && argumentList.Arguments.Count is > 0 and <= 2
            && argumentList.Arguments[0].Expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression);
    }

    internal static bool HasModifier(FieldDeclarationSyntax field, SyntaxKind kind)
    {
        foreach (var modifier in field.Modifiers)
        {
            if (modifier.IsKind(kind))
            {
                return true;
            }
        }

        return false;
    }
}
