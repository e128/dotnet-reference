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
        var optionsType = context.Compilation.GetTypeByMetadataName(OptionsMetadataName);

        if (optionsType is null)
        {
            return;
        }

        var serializerType = context.Compilation.GetTypeByMetadataName(SerializerMetadataName);

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeAssignment(ctx, optionsType, serializerType),
            SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeConvertersCall(ctx, optionsType, serializerType),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeAssignment(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol optionsType,
        INamedTypeSymbol? serializerType)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (assignment.Left is not MemberAccessExpressionSyntax access
            || !TryGetTrackedSymbol(access.Expression, optionsType, context, out var symbol))
        {
            return;
        }

        ReportIfUsedBefore(context, assignment, symbol, serializerType);
    }

    private static void AnalyzeConvertersCall(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol optionsType,
        INamedTypeSymbol? serializerType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax call
            || !IsConverterList(call, optionsType, context)
            || call.Expression is not MemberAccessExpressionSyntax converters
            || !TryGetTrackedSymbol(converters.Expression, optionsType, context, out var symbol))
        {
            return;
        }

        ReportIfUsedBefore(context, invocation, symbol, serializerType);
    }

    /// <summary>
    ///     Returns true when the call mutates <c lang="csharp">JsonSerializerOptions.Converters</c>.
    /// </summary>
    private static bool IsConverterList(
        MemberAccessExpressionSyntax call,
        INamedTypeSymbol optionsType,
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
               && SymbolEqualityComparer.Default.Equals(property.ContainingType, optionsType);
    }

    /// <summary>
    ///     True when the expression binds to a local, a parameter, or a field of the options type. A
    ///     property is excluded, because the getter can return a new instance on each read.
    /// </summary>
    private static bool TryGetTrackedSymbol(
        ExpressionSyntax receiver,
        INamedTypeSymbol optionsType,
        SyntaxNodeAnalysisContext context,
        [NotNullWhen(true)] out ISymbol? symbol)
    {
        symbol = context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol;

        return symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol
               && SymbolEqualityComparer.Default.Equals(
                   optionsType,
                   context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type);
    }

    private static void ReportIfUsedBefore(
        SyntaxNodeAnalysisContext context,
        SyntaxNode mutation,
        ISymbol symbol,
        INamedTypeSymbol? serializerType)
    {
        var body = FindEnclosingBody(mutation);

        if (body is null)
        {
            return;
        }

        foreach (var node in body.DescendantNodes())
        {
            if (node.SpanStart >= mutation.SpanStart)
            {
                continue;
            }

            if (node is InvocationExpressionSyntax invocation
                && IsSerializerCall(invocation, serializerType, context)
                && PassesSymbol(invocation, symbol, context))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, mutation.GetLocation()));
                return;
            }
        }
    }

    private static bool IsSerializerCall(
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol? serializerType,
        SyntaxNodeAnalysisContext context)
    {
        return serializerType is not null
               && context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
               is IMethodSymbol method
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, serializerType);
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

    private static BlockSyntax? FindEnclosingBody(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is MethodDeclarationSyntax method)
            {
                return method.Body;
            }

            if (ancestor is ConstructorDeclarationSyntax constructor)
            {
                return constructor.Body;
            }

            if (ancestor is LocalFunctionStatementSyntax localFunction)
            {
                return localFunction.Body;
            }

            if (ancestor is AccessorDeclarationSyntax accessor)
            {
                return accessor.Body;
            }

            if (ancestor is AnonymousFunctionExpressionSyntax anonymous)
            {
                return anonymous.Block;
            }
        }

        return null;
    }
}
