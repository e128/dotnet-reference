using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
/// E128026: Flags <c>new HashSet&lt;T&gt;(...).ToFrozenSet(...)</c> — the intermediate HashSet allocation
/// is unnecessary. The collection can be passed directly to <c>ToFrozenSet()</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantHashSetInFrozenSetE128Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128026";

    private const string HashSetMetadataName = "System.Collections.Generic.HashSet`1";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Redundant HashSet allocation in FrozenSet creation",
        messageFormat: "Intermediate HashSet<{0}> allocation is unnecessary — pass the collection directly to ToFrozenSet()",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "Creating a HashSet<T> only to immediately call .ToFrozenSet() allocates an intermediate collection " +
            "that is immediately discarded. Pass the collection expression or array directly to ToFrozenSet() instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var hashSetType = compilationContext.Compilation.GetTypeByMetadataName(HashSetMetadataName);
            if (hashSetType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, hashSetType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol hashSetType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsToFrozenSetCall(invocation))
        {
            return;
        }

        var receiver = GetReceiverExpression(invocation);
        if (receiver is null)
        {
            return;
        }

        if (!IsHashSetCreation(context, receiver, hashSetType, out var elementTypeName))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            elementTypeName));
    }

    private static bool IsToFrozenSetCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && string.Equals(memberAccess.Name.Identifier.Text, "ToFrozenSet", StringComparison.Ordinal);
    }

    private static ExpressionSyntax? GetReceiverExpression(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess ? memberAccess.Expression : null;
    }

    private static bool IsHashSetCreation(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax receiver,
        INamedTypeSymbol hashSetType,
        out string elementTypeName)
    {
        elementTypeName = string.Empty;

        if (receiver is not ObjectCreationExpressionSyntax creation)
        {
            return false;
        }

        var typeInfo = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol createdType || !createdType.IsGenericType)
        {
            return false;
        }

        if (!SymbolEqualityComparer.Default.Equals(createdType.OriginalDefinition, hashSetType))
        {
            return false;
        }

        elementTypeName = createdType.TypeArguments[0].ToDisplayString();
        return true;
    }
}
