using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128196: Reports a write to a <c lang="csharp">JsonSerializerOptions</c> instance after the
///     same body passed the instance to <c lang="csharp">JsonSerializer</c>. The serializer freezes
///     the instance on first use, so the write throws
///     <c lang="csharp">InvalidOperationException</c>.
/// </summary>
/// <remarks>
///     The rule reads one body at a time. A use in one method and a write in another method stay
///     silent, because the rule does not follow a field across callers.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JsonSerializerOptionsMutationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128196";

    internal const string OptionsMetadataName = "System.Text.Json.JsonSerializerOptions";

    internal const string SerializerMetadataName = "System.Text.Json.JsonSerializer";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "JsonSerializerOptions mutated after first use",
        "Set every JsonSerializerOptions member before the first serialize or deserialize call",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "JsonSerializer freezes a JsonSerializerOptions instance on first use. A later write to a " +
        "member of that instance throws InvalidOperationException.");

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
        var optionsTypes = MetadataTypes.Find(context.Compilation, OptionsMetadataName);

        if (optionsTypes.IsEmpty)
        {
            return;
        }

        var serializerTypes = MetadataTypes.Find(context.Compilation, SerializerMetadataName);

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeAssignment(ctx, optionsTypes, serializerTypes),
            SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeConvertersCall(ctx, optionsTypes, serializerTypes),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeAssignment(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> optionsTypes,
        ImmutableArray<INamedTypeSymbol> serializerTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (assignment.Left is not MemberAccessExpressionSyntax access
            || !TryGetTrackedSymbol(access.Expression, optionsTypes, context, out var symbol))
        {
            return;
        }

        ReportIfUsedBefore(context, assignment, symbol, serializerTypes);
    }

    private static void AnalyzeConvertersCall(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> optionsTypes,
        ImmutableArray<INamedTypeSymbol> serializerTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax call
            || !IsConverterList(call, optionsTypes, context)
            || call.Expression is not MemberAccessExpressionSyntax converters
            || !TryGetTrackedSymbol(converters.Expression, optionsTypes, context, out var symbol))
        {
            return;
        }

        ReportIfUsedBefore(context, invocation, symbol, serializerTypes);
    }

    /// <summary>
    ///     Returns true when the call mutates <c lang="csharp">JsonSerializerOptions.Converters</c>.
    /// </summary>
    private static bool IsConverterList(
        MemberAccessExpressionSyntax call,
        ImmutableArray<INamedTypeSymbol> optionsTypes,
        SyntaxNodeAnalysisContext context)
    {
        var name = call.Name.Identifier.ValueText;

        var mutates = string.Equals(name, "Add", StringComparison.Ordinal)
                      || string.Equals(name, "Insert", StringComparison.Ordinal)
                      || string.Equals(name, "Clear", StringComparison.Ordinal)
                      || string.Equals(name, "Remove", StringComparison.Ordinal)
                      || string.Equals(name, "RemoveAt", StringComparison.Ordinal);

        return mutates
               && call.Expression is MemberAccessExpressionSyntax converters
               && string.Equals(converters.Name.Identifier.ValueText, "Converters", StringComparison.Ordinal)
               && context.SemanticModel.GetSymbolInfo(converters, context.CancellationToken).Symbol
                   is IPropertySymbol property
               && MetadataTypes.Contains(optionsTypes, property.ContainingType);
    }

    /// <summary>
    ///     True when the expression binds to a local, a parameter, or a field of the options type. A
    ///     property is excluded, because the getter can return a new instance on each read.
    /// </summary>
    private static bool TryGetTrackedSymbol(
        ExpressionSyntax receiver,
        ImmutableArray<INamedTypeSymbol> optionsTypes,
        SyntaxNodeAnalysisContext context,
        [NotNullWhen(true)] out ISymbol? symbol)
    {
        symbol = context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol;

        return symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol
               && MetadataTypes.Contains(
                   optionsTypes,
                   context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type);
    }

    private static void ReportIfUsedBefore(
        SyntaxNodeAnalysisContext context,
        SyntaxNode mutation,
        ISymbol symbol,
        ImmutableArray<INamedTypeSymbol> serializerTypes)
    {
        var body = EnclosingBody.Find(mutation);

        if (body is null)
        {
            return;
        }

        var used = false;

        foreach (var node in body.DescendantNodes())
        {
            if (node.SpanStart >= mutation.SpanStart)
            {
                break;
            }

            if (IsInNestedFunction(node, body) || ExclusiveBranches.Are(node, mutation))
            {
                continue;
            }

            // A new instance assigned to the same variable starts a fresh, unfrozen options object.
            if (node is AssignmentExpressionSyntax assignment
                && SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol,
                    symbol))
            {
                used = false;
            }
            else if (node is InvocationExpressionSyntax invocation
                     && IsSerializerCall(invocation, serializerTypes, context)
                     && PassesSymbol(invocation, symbol, context))
            {
                used = true;
            }
        }

        if (used)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, mutation.GetLocation()));
        }
    }

    // A lambda or a local function can run later or never, so a call inside one is not a use
    // that precedes the write.
    private static bool IsInNestedFunction(SyntaxNode node, SyntaxNode body)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ReferenceEquals(ancestor, body))
            {
                return false;
            }

            if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSerializerCall(
        InvocationExpressionSyntax invocation,
        ImmutableArray<INamedTypeSymbol> serializerTypes,
        SyntaxNodeAnalysisContext context)
    {
        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
               is IMethodSymbol method
               && MetadataTypes.Contains(serializerTypes, method.ContainingType);
    }

    private static bool PassesSymbol(
        InvocationExpressionSyntax invocation,
        ISymbol symbol,
        SyntaxNodeAnalysisContext context)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(argument.Expression, context.CancellationToken).Symbol,
                    symbol))
            {
                return true;
            }
        }

        return false;
    }
}
