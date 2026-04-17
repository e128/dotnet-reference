using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrimaryConstructorBackingFieldAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128017";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use primary constructor parameter directly",
        "Field '{0}' is an identity assignment from primary constructor parameter '{1}' — use the parameter directly in method bodies",
        "Design",
        DiagnosticSeverity.Warning,
        true,
        "When a primary constructor parameter is assigned unchanged to a backing field " +
        "(e.g. 'private readonly IFoo _foo = foo;'), the field is redundant. " +
        "Use the primary constructor parameter directly in method bodies instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;

        var typeDeclaration = field.Parent as TypeDeclarationSyntax;
        if (typeDeclaration is null or StructDeclarationSyntax)
        {
            return;
        }

        if (typeDeclaration.ParameterList is null)
        {
            return;
        }

        if (!IsReadonly(field))
        {
            return;
        }

        foreach (var variable in field.Declaration.Variables)
        {
            if (variable.Initializer?.Value is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            var parameterName = identifier.Identifier.ValueText;
            if (!IsPrimaryConstructorParameter(typeDeclaration, parameterName))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, variable.Identifier.GetLocation(), variable.Identifier.ValueText, parameterName));
        }
    }

    private static bool IsReadonly(FieldDeclarationSyntax field)
    {
        foreach (var modifier in field.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPrimaryConstructorParameter(TypeDeclarationSyntax typeDeclaration, string name)
    {
        foreach (var parameter in typeDeclaration.ParameterList!.Parameters)
        {
            if (string.Equals(parameter.Identifier.ValueText, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
