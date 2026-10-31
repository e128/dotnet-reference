using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Design;

/// <summary>
///     E128032: Flags <c lang="csharp">services.AddSingleton&lt;MyService&gt;()</c> when <c lang="csharp">MyService</c>
///     implements a non-marker interface. Concrete-only DI registrations prevent interface-based
///     resolution, making the service untestable and tightly coupled.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConcreteOnlyDiRegistrationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128032";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Concrete-only DI registration with available interface",
        "'{0}' implements '{1}'. Register as Add{2}<{3}, {0}>() for testability and loose coupling.",
        "Design",
        DiagnosticSeverity.Warning,
        true,
        "Registering a concrete type via AddSingleton<T>(), AddScoped<T>(), or AddTransient<T>() " +
        "when T implements a non-marker interface prevents consumers from resolving by interface. " +
        "Use Add{Lifetime}<IFoo, T>() instead.");

    private static readonly HashSet<string> TargetMethodNames = new(StringComparer.Ordinal)
    {
        "AddSingleton",
        "AddScoped",
        "AddTransient"
    };

    // The code fix reads this set, so it is shared. An immutable set keeps it read-only.
    internal static readonly ImmutableHashSet<string> MarkerInterfaces =
        ImmutableHashSet.Create(StringComparer.Ordinal, "System.IDisposable", "System.IAsyncDisposable");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!TryGetMethodName(invocation, out var nameNode))
        {
            return;
        }

        if (!TargetMethodNames.Contains(nameNode.Identifier.Text))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (methodSymbol.TypeArguments.Length != 1)
        {
            return;
        }

        ReportIfConcreteOnly(context, invocation, nameNode, methodSymbol);
    }

    private static bool TryGetMethodName(InvocationExpressionSyntax invocation, [NotNullWhen(true)] out SimpleNameSyntax? nameNode)
    {
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax m:
                nameNode = m.Name;
                return true;
            case IdentifierNameSyntax i:
                nameNode = i;
                return true;
            default:
                nameNode = null;
                return false;
        }
    }

    private static void ReportIfConcreteOnly(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        SimpleNameSyntax nameNode,
        IMethodSymbol methodSymbol)
    {
        var typeArg = methodSymbol.TypeArguments[0];

        if (typeArg.TypeKind is TypeKind.Interface or TypeKind.TypeParameter or TypeKind.Delegate || typeArg.IsAbstract)
        {
            return;
        }

        var nonMarkerInterfaces = typeArg.AllInterfaces
            .Where(i => !MarkerInterfaces.Contains(i.ToDisplayString()))
            .ToList();

        if (nonMarkerInterfaces.Count == 0)
        {
            return;
        }

        // Skip if the containing scope has a forwarding registration or typed HttpClient for this type.
        // Check BlockSyntax (method body) first, then CompilationUnitSyntax (top-level statements).
        var typeName = typeArg.Name;
        var scope = (SyntaxNode?)invocation.FirstAncestorOrSelf<BlockSyntax>()
                    ?? invocation.SyntaxTree.GetRoot(context.CancellationToken);
        if (HasForwardingRegistration(scope, typeName) || HasTypedHttpClientRegistration(scope, typeName))
        {
            return;
        }

        var lifetime = nameNode.Identifier.Text.Substring(3);
        // string.Join already returns the single element unchanged, so Count == 1 needs no branch.
        var interfaceNames = string.Join(", ", nonMarkerInterfaces.Select(i => i.Name));
        // The suggested call must carry the type arguments so it matches what the code fix writes.
        var suggestedType = nonMarkerInterfaces[0]
            .ToMinimalDisplayString(context.SemanticModel, invocation.SpanStart);

        var diagnosticSpan = TextSpan.FromBounds(nameNode.SpanStart, invocation.Span.End);
        var diagnosticLocation = Location.Create(invocation.SyntaxTree, diagnosticSpan);

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            diagnosticLocation,
            typeArg.Name,
            interfaceNames,
            lifetime,
            suggestedType));
    }

    /// <summary>
    ///     Returns true if the block contains a forwarding registration like
    ///     <c lang="csharp">services.AddSingleton&lt;IFoo&gt;(sp =&gt; sp.GetRequiredService&lt;ConcreteType&gt;())</c>.
    /// </summary>
    private static bool HasForwardingRegistration(SyntaxNode scope, string concreteTypeName)
    {
        return ScopeContainsGenericCall(scope, "GetRequiredService", concreteTypeName);
    }

    /// <summary>
    ///     Returns true if the scope contains <c lang="csharp">AddHttpClient&lt;ConcreteType&gt;()</c>.
    /// </summary>
    private static bool HasTypedHttpClientRegistration(SyntaxNode scope, string concreteTypeName)
    {
        return ScopeContainsGenericCall(scope, "AddHttpClient", concreteTypeName);
    }

    // Matches the call shape directly. The old text search rebuilt the scope text for every
    // flagged registration, which is quadratic on a large block and allocates the whole scope.
    private static bool ScopeContainsGenericCall(SyntaxNode scope, string methodName, string typeName)
    {
        foreach (var invocation in scope.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax generic })
            {
                continue;
            }

            if (!string.Equals(generic.Identifier.ValueText, methodName, StringComparison.Ordinal)
                || generic.TypeArgumentList.Arguments.Count != 1
                || generic.TypeArgumentList.Arguments[0] is not IdentifierNameSyntax typeArgument)
            {
                continue;
            }

            if (string.Equals(typeArgument.Identifier.ValueText, typeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
