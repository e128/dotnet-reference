using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyStringLiteralAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128002";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use string.Empty instead of \"\"",
        "Replace empty string literal \"\" with string.Empty",
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
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.StringLiteralExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;

        // Only interested in empty string literals.
        if (literal.Token.ValueText.Length > 0)
        {
            return;
        }

        // Skip compile-time constant contexts where string.Empty cannot be used.
        if (IsAttributeArgument(literal) || IsConstContext(literal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, literal.GetLocation()));
    }

    // Returns true if the node is inside an attribute argument list.
    // string.Empty is not a compile-time constant and cannot appear in attribute arguments.
    private static bool IsAttributeArgument(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is AttributeArgumentSyntax)
            {
                return true;
            }
        }

        return false;
    }

    // Returns true if the node is in a context that requires a compile-time constant:
    //   - const field: private const string K = ""
    //   - const local: const string c = ""
    //   - default parameter value: void M(string s = "")
    private static bool IsConstContext(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is VariableDeclarationSyntax decl)
            {
                var parent = decl.Parent;
                if (parent is FieldDeclarationSyntax field
                    && field.Modifiers.Any(SyntaxKind.ConstKeyword))
                {
                    return true;
                }

                if (parent is LocalDeclarationStatementSyntax local
                    && local.Modifiers.Any(SyntaxKind.ConstKeyword))
                {
                    return true;
                }
            }

            // Default parameter value: void M(string s = "")
            if (ancestor is ParameterSyntax)
            {
                return true;
            }
        }

        return false;
    }
}
