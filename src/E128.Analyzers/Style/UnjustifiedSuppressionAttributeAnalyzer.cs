using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128173: Flags <c lang="csharp">[SuppressMessage]</c> and <c lang="csharp">[ExcludeFromCodeCoverage]</c> with no
///     <c lang="csharp">Justification</c>. The reason lives in the attribute so a later reader finds it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnjustifiedSuppressionAttributeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128173";

    private static readonly string[] CheckedAttributes =
    [
        "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
        "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute"
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Justify every suppression attribute",
        "[{0}] has no Justification: state why the suppression is safe",
        "E128-Style",
        DiagnosticSeverity.Warning,
        true,
        "A suppression or coverage exclusion with no reason cannot be reviewed or removed later. Set the Justification argument.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var checkedTypes = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var metadataName in CheckedAttributes)
        {
            // Two references can declare the same type, so the lookup keeps every match.
            foreach (var type in context.Compilation.GetTypesByMetadataName(metadataName))
            {
                // ExcludeFromCodeCoverageAttribute.Justification exists only on .NET 5 and later.
                if (HasJustificationProperty(type))
                {
                    checkedTypes.Add(type);
                }
            }
        }

        if (checkedTypes.Count == 0)
        {
            return;
        }

        var types = checkedTypes.ToImmutable();

        context.RegisterSyntaxNodeAction(ctx => AnalyzeAttribute(ctx, types), SyntaxKind.Attribute);
    }

    private static bool HasJustificationProperty(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers("Justification"))
        {
            if (member is IPropertySymbol)
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> checkedTypes)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !IsCheckedType(checkedTypes, constructor.ContainingType))
        {
            return;
        }

        if (!HasJustification(attribute, context))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation(), constructor.ContainingType.Name.Replace("Attribute", string.Empty)));
        }
    }

    private static bool IsCheckedType(ImmutableArray<INamedTypeSymbol> checkedTypes, ITypeSymbol type)
    {
        foreach (var candidate in checkedTypes)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasJustification(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        if (attribute.ArgumentList is null)
        {
            return false;
        }

        foreach (var argument in attribute.ArgumentList.Arguments)
        {
            if (argument.NameEquals is { } nameEquals
                && string.Equals(nameEquals.Name.Identifier.ValueText, "Justification", StringComparison.Ordinal))
            {
                var constant = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);

                // A null or blank constant gives no reason. A non-constant expression counts as a reason.
                return !constant.HasValue || (constant.Value is string text && !string.IsNullOrWhiteSpace(text));
            }
        }

        return false;
    }
}
