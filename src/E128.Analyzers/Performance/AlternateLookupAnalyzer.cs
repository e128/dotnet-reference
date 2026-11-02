using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128133: A lookup builds a string from a span or a char array only to use it as a key.
///     <c lang="csharp">GetAlternateLookup&lt;ReadOnlySpan&lt;char&gt;&gt;()</c> compares the span directly.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AlternateLookupAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128133";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use an alternate lookup instead of a temporary string key",
        "The lookup builds a string from a span. Use GetAlternateLookup<ReadOnlySpan<char>> instead.",
        "Performance",
        DiagnosticSeverity.Warning,
        true,
        "A key of the form 'new string(span)' allocates a string on every lookup. " +
        "GetAlternateLookup<ReadOnlySpan<char>>() compares the span in place. It works only " +
        "when the collection uses the default or an ordinal comparer.");

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
            var stringType = start.Compilation.GetSpecialType(SpecialType.System_String);
            var charType = start.Compilation.GetSpecialType(SpecialType.System_Char);
            var readOnlySpan = start.Compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");

            if (stringType.TypeKind == TypeKind.Error
                || charType.TypeKind == TypeKind.Error
                || readOnlySpan is null)
            {
                return;
            }

            var lookup = new LookupContext(
                start.Compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2"),
                start.Compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentDictionary`2"),
                start.Compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1"),
                readOnlySpan.Construct(charType));

            if (lookup.Dictionary is null && lookup.HashSet is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => AnalyzeElementAccess(node, lookup),
                SyntaxKind.ElementAccessExpression);

            start.RegisterSyntaxNodeAction(
                node => AnalyzeInvocation(node, lookup),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context, LookupContext lookup)
    {
        var access = (ElementAccessExpressionSyntax)context.Node;

        if (access.ArgumentList.Arguments.Count != 1
            || access.Expression is not IdentifierNameSyntax receiver
            || !IsStringKeyed(receiver, context.SemanticModel, lookup, allowsIndexer: true, context.CancellationToken)
            || FindAllocatingKey(access.ArgumentList.Arguments[0].Expression, context.SemanticModel, lookup,
                context.CancellationToken) is not { } key
            || !HasOrdinalComparer(receiver, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, key.GetLocation()));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, LookupContext lookup)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Only a method whose first argument is a key reports. A member such as
        // ContainsValue takes the value position, so its argument never proves
        // that the dictionary keys come from this span.
        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax receiver,
                Name.Identifier.ValueText: var methodName
            }
            || !IsKeyLookupMethod(methodName)
            || !IsStringKeyed(receiver, context.SemanticModel, lookup, allowsIndexer: false, context.CancellationToken)
            || invocation.ArgumentList.Arguments.Count == 0
            || FindAllocatingKey(invocation.ArgumentList.Arguments[0].Expression, context.SemanticModel, lookup,
                context.CancellationToken) is not { } key
            || !HasOrdinalComparer(receiver, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, key.GetLocation()));
    }

    // Members whose first argument is a key on the tracked collection types. ContainsValue,
    // which takes a value, is never a lookup key.
    private static bool IsKeyLookupMethod(string methodName)
    {
        return methodName is "Add" or "TryAdd" or "TryGetValue" or "ContainsKey" or "Contains"
            or "Remove" or "TryRemove" or "GetOrAdd" or "AddOrUpdate" or "TryUpdate";
    }

    private static bool IsStringKeyed(
        IdentifierNameSyntax receiver,
        SemanticModel model,
        LookupContext lookup,
        bool allowsIndexer,
        System.Threading.CancellationToken cancellationToken)
    {
        if (model.GetTypeInfo(receiver, cancellationToken).Type is not INamedTypeSymbol type)
        {
            return false;
        }

        var definition = type.OriginalDefinition;

        return SymbolEqualityComparer.Default.Equals(definition, lookup.Dictionary)
                || SymbolEqualityComparer.Default.Equals(definition, lookup.ConcurrentDictionary)
            ? type.TypeArguments.Length == 2 && type.TypeArguments[0].SpecialType == SpecialType.System_String
            : !allowsIndexer
                && SymbolEqualityComparer.Default.Equals(definition, lookup.HashSet)
                && type.TypeArguments.Length == 1
                && type.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static ObjectCreationExpressionSyntax? FindAllocatingKey(
        ExpressionSyntax expression,
        SemanticModel model,
        LookupContext lookup,
        System.Threading.CancellationToken cancellationToken)
    {
        if (expression is not ObjectCreationExpressionSyntax creation
            || creation.ArgumentList is null
            || creation.ArgumentList.Arguments.Count != 1
            || model.GetTypeInfo(creation, cancellationToken).Type is not { SpecialType: SpecialType.System_String })
        {
            return null;
        }

        var argument = model.GetTypeInfo(creation.ArgumentList.Arguments[0].Expression, cancellationToken).Type;

        return SymbolEqualityComparer.Default.Equals(argument, lookup.SpanOfChar)
            || argument is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Char }
            ? creation
            : null;
    }

    /// <summary>
    ///     The alternate lookup throws when the comparer is culture aware. Accept only a type whose
    ///     initializer proves the default or the ordinal comparer.
    /// </summary>
    private static bool HasOrdinalComparer(
        IdentifierNameSyntax receiver,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        var declarator = model.GetSymbolInfo(receiver, cancellationToken).Symbol switch
        {
            ILocalSymbol local => local.DeclaringSyntaxReferences.FirstOrDefault()
                ?.GetSyntax(cancellationToken) as VariableDeclaratorSyntax,
            IFieldSymbol field => field.DeclaringSyntaxReferences.FirstOrDefault()
                ?.GetSyntax(cancellationToken) as VariableDeclaratorSyntax,
            _ => null
        };

        switch (declarator?.Initializer?.Value)
        {
            case null:
                return false;
            case CollectionExpressionSyntax:
                return true;
            case BaseObjectCreationExpressionSyntax creation:
                var arguments = creation.ArgumentList?.Arguments ?? default;

                return arguments.Count == 0
                    || (arguments.Count == 1 && IsOrdinalComparer(arguments[0].Expression, model, cancellationToken));
            default:
                return false;
        }
    }

    private static bool IsOrdinalComparer(
        ExpressionSyntax expression,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(expression, cancellationToken).Symbol is IPropertySymbol property
            && (string.Equals(property.ContainingType.ToDisplayString(), "System.StringComparer", StringComparison.Ordinal)
                ? string.Equals(property.Name, "Ordinal", StringComparison.Ordinal)
                : string.Equals(property.Name, "Default", StringComparison.Ordinal)
                    && property.ContainingType is { Name: "EqualityComparer", TypeArguments.Length: 1 } comparer
                    && comparer.TypeArguments[0].SpecialType == SpecialType.System_String);
    }

    internal sealed class LookupContext
    {
        public LookupContext(
            INamedTypeSymbol? dictionary,
            INamedTypeSymbol? concurrentDictionary,
            INamedTypeSymbol? hashSet,
            INamedTypeSymbol spanOfChar)
        {
            Dictionary = dictionary;
            ConcurrentDictionary = concurrentDictionary;
            HashSet = hashSet;
            SpanOfChar = spanOfChar;
        }

        public INamedTypeSymbol? Dictionary { get; }

        public INamedTypeSymbol? ConcurrentDictionary { get; }

        public INamedTypeSymbol? HashSet { get; }

        public INamedTypeSymbol SpanOfChar { get; }
    }
}
