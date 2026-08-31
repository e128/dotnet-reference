using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128074: Flags properties in <c lang="csharp">readonly struct</c> declarations that have only a
///     <c lang="csharp">get</c> accessor without <c lang="csharp">init</c>. Without <c lang="csharp">init</c>, the immutability
///     contract is implicit rather than explicit at the language level.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadonlyStructInitPropertyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128074";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Readonly struct property should use init accessor",
        "Property '{0}' in readonly struct '{1}' has only a get accessor — use 'init' to make immutability explicit",
        "Design",
        DiagnosticSeverity.Warning,
        true,
        "Properties in readonly structs that have only a get accessor should use 'init' instead. " +
        "This makes the immutability contract explicit at the language level and enables object initializer syntax.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;

        if (property.ExpressionBody is not null)
        {
            return;
        }

        if (property.AccessorList is null)
        {
            return;
        }

        if (property.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        var containingType = property.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingType is not StructDeclarationSyntax structDecl)
        {
            return;
        }

        if (!structDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            return;
        }

        if (!IsGetOnlyWithoutInit(property.AccessorList))
        {
            return;
        }

        var structName = structDecl.Identifier.ValueText;
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            property.Identifier.GetLocation(),
            property.Identifier.ValueText,
            structName));
    }

    private static bool IsGetOnlyWithoutInit(AccessorListSyntax accessorList)
    {
        var hasGet = false;
        foreach (var accessor in accessorList.Accessors)
        {
            if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
            {
                hasGet = true;
            }
            else if (accessor.IsKind(SyntaxKind.InitAccessorDeclaration)
                     || accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                return false;
            }
        }

        return hasGet;
    }
}
