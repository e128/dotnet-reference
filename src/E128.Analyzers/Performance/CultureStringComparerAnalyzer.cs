using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128132: A string dictionary or set uses a culture-sensitive comparer. An ordinal comparer is
///     faster and gives a stable result across cultures.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CultureStringComparerAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128132";
    internal const string ReplacementKey = "Replacement";

    internal static readonly ImmutableDictionary<string, string> ReplacementByComparer =
        ImmutableDictionary.CreateRange(
            StringComparer.Ordinal,
            [
                new KeyValuePair<string, string>("CurrentCulture", "Ordinal"),
                new KeyValuePair<string, string>("CurrentCultureIgnoreCase", "OrdinalIgnoreCase"),
                new KeyValuePair<string, string>("InvariantCulture", "Ordinal"),
                new KeyValuePair<string, string>("InvariantCultureIgnoreCase", "OrdinalIgnoreCase")
            ]);

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use an ordinal comparer for a string collection",
        "StringComparer.{0} is culture-sensitive. Use StringComparer.{1}.",
        "Performance",
        DiagnosticSeverity.Warning,
        true,
        "The current-culture and invariant-culture comparers cost more than an ordinal comparer " +
        "and produce a different result per culture. Use StringComparer.Ordinal, or " +
        "StringComparer.OrdinalIgnoreCase when case must not matter.");

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
            var collections = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

            AddCollection(start.Compilation, collections, "System.Collections.Generic.Dictionary`2");
            AddCollection(start.Compilation, collections, "System.Collections.Generic.HashSet`1");
            AddCollection(start.Compilation, collections, "System.Collections.Concurrent.ConcurrentDictionary`2");
            AddCollection(start.Compilation, collections, "System.Collections.Generic.SortedDictionary`2");
            AddCollection(start.Compilation, collections, "System.Collections.Generic.SortedSet`1");

            var stringComparer = start.Compilation.GetTypeByMetadataName("System.StringComparer");
            if (collections.Count == 0 || stringComparer is null)
            {
                return;
            }

            var tracked = collections.ToImmutable();

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, tracked, stringComparer),
                SyntaxKind.ObjectCreationExpression);

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, tracked, stringComparer),
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    private static void AddCollection(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol>.Builder collections,
        string metadataName)
    {
        if (compilation.GetTypeByMetadataName(metadataName) is { } type)
        {
            collections.Add(type);
        }
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> collections,
        INamedTypeSymbol stringComparer)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;

        if (creation.ArgumentList is null)
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not INamedTypeSymbol type)
        {
            return;
        }

        if (!IsTrackedCollection(type, collections))
        {
            return;
        }

        foreach (var argument in creation.ArgumentList.Arguments)
        {
            if (FindCultureComparer(argument.Expression, context.SemanticModel, stringComparer, context.CancellationToken)
                is not { } comparer)
            {
                continue;
            }

            var replacement = ReplacementByComparer[comparer];

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                argument.Expression.GetLocation(),
                ImmutableDictionary.CreateRange(
                    [
                        new KeyValuePair<string, string?>(ReplacementKey, replacement)
                    ]),
                comparer,
                replacement));
        }
    }

    private static bool IsTrackedCollection(INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> collections)
    {
        if (type.TypeArguments.Length == 0
            || type.TypeArguments[0].SpecialType != SpecialType.System_String)
        {
            return false;
        }

        foreach (var candidate in collections)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, candidate))
            {
                return true;
            }
        }

        return false;
    }

    internal static string? FindCultureComparer(
        ExpressionSyntax expression,
        SemanticModel model,
        INamedTypeSymbol stringComparer,
        CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(expression, cancellationToken).Symbol is IPropertySymbol property
            && SymbolEqualityComparer.Default.Equals(property.ContainingType, stringComparer)
            && ReplacementByComparer.ContainsKey(property.Name)
                ? property.Name
                : null;
    }
}
