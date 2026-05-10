using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128073: Flags test methods (<c>[Fact]</c> or <c>[Theory]</c>) that lack a
///     <c>[Trait("Category", "...")]</c> attribute. Without a category trait, the test
///     may be silently excluded from filtered CI runs.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingTraitCategoryAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128073";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Test method missing [Trait(\"Category\", ...)] attribute",
        "Test method '{0}' is missing a [Trait(\"Category\", ...)] attribute — tests without a category may be excluded from CI",
        "Testing",
        DiagnosticSeverity.Warning,
        true,
        "Every [Fact] and [Theory] method should have a [Trait(\"Category\", \"...\")] attribute " +
        "so the test runner can filter by category (CI, Docker, Manual).");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (!IsFactOrTheoryAttribute(attribute, context))
        {
            return;
        }

        var method = attribute.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return;
        }

        if (HasTraitCategoryOnMethod(method, context) || HasTraitCategoryOnClass(method, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation(), method.Identifier.ValueText));
    }

    private static bool IsFactOrTheoryAttribute(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        var name = attribute.Name.ToString();
        if (!string.Equals(name, "Fact", StringComparison.Ordinal)
            && !string.Equals(name, "Theory", StringComparison.Ordinal)
            && !string.Equals(name, "FactAttribute", StringComparison.Ordinal)
            && !string.Equals(name, "TheoryAttribute", StringComparison.Ordinal))
        {
            return false;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol;
        return symbol is IMethodSymbol ctor
               && ctor.ContainingType is { } containingType
               && (string.Equals(containingType.Name, "FactAttribute", StringComparison.Ordinal)
                   || string.Equals(containingType.Name, "TheoryAttribute", StringComparison.Ordinal))
               && IsXunitNamespace(containingType.ContainingNamespace);
    }

    private static bool IsXunitNamespace(INamespaceSymbol? ns)
    {
        return ns is not null && string.Equals(ns.ToDisplayString(), "Xunit", StringComparison.Ordinal);
    }

    private static bool HasTraitCategoryOnMethod(MethodDeclarationSyntax method, SyntaxNodeAnalysisContext context)
    {
        return HasTraitCategory(method.AttributeLists, context);
    }

    private static bool HasTraitCategoryOnClass(MethodDeclarationSyntax method, SyntaxNodeAnalysisContext context)
    {
        var typeDecl = method.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        return typeDecl is not null && HasTraitCategory(typeDecl.AttributeLists, context);
    }

    private static bool HasTraitCategory(SyntaxList<AttributeListSyntax> attributeLists, SyntaxNodeAnalysisContext context)
    {
        foreach (var attrList in attributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                if (IsTraitCategoryAttribute(attr, context))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTraitCategoryAttribute(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        var name = attribute.Name.ToString();
        if (!string.Equals(name, "Trait", StringComparison.Ordinal)
            && !string.Equals(name, "TraitAttribute", StringComparison.Ordinal))
        {
            return false;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol ctor
            || ctor.ContainingType is not { } containingType
            || !string.Equals(containingType.Name, "TraitAttribute", StringComparison.Ordinal)
            || !IsXunitNamespace(containingType.ContainingNamespace))
        {
            return false;
        }

        if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count < 1)
        {
            return false;
        }

        var firstArg = attribute.ArgumentList.Arguments[0];
        var constantValue = context.SemanticModel.GetConstantValue(firstArg.Expression, context.CancellationToken);
        return constantValue.HasValue
               && constantValue.Value is string s
               && string.Equals(s, "Category", StringComparison.Ordinal);
    }
}
