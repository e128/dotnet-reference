using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Design;

/// <summary>
/// E128032: Flags <c>services.AddSingleton&lt;MyService&gt;()</c> when <c>MyService</c>
/// implements a non-marker interface. Concrete-only DI registrations prevent interface-based
/// resolution, making the service untestable and tightly coupled.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConcreteOnlyDiRegistrationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128032";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Concrete-only DI registration with available interface",
        messageFormat: "'{0}' implements '{1}' — register as Add{2}<{1}, {0}>() for testability and loose coupling",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Registering a concrete type via AddSingleton<T>(), AddScoped<T>(), or AddTransient<T>() " +
            "when T implements a non-marker interface prevents consumers from resolving by interface. " +
            "Use Add{Lifetime}<IFoo, T>() instead.");

    private static readonly HashSet<string> TargetMethodNames = new(StringComparer.Ordinal)
    {
        "AddSingleton",
        "AddScoped",
        "AddTransient",
    };

    private static readonly HashSet<string> MarkerInterfaces = new(StringComparer.Ordinal)
    {
        "System.IDisposable",
        "System.IAsyncDisposable",
    };

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

    private static bool TryGetMethodName(InvocationExpressionSyntax invocation, out SimpleNameSyntax nameNode)
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
                nameNode = null!;
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

        if (InheritsFromDelegate(typeArg))
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
        var interfaceNames = string.Join(", ", nonMarkerInterfaces.Select(i => i.Name));

        var diagnosticSpan = TextSpan.FromBounds(nameNode.SpanStart, invocation.Span.End);
        var diagnosticLocation = Location.Create(invocation.SyntaxTree, diagnosticSpan);

        context.ReportDiagnostic(Diagnostic.Create(Rule, diagnosticLocation, typeArg.Name, interfaceNames, lifetime));
    }

    /// <summary>
    /// Returns true if the block contains a forwarding registration like
    /// <c>services.AddSingleton&lt;IFoo&gt;(sp =&gt; sp.GetRequiredService&lt;ConcreteType&gt;())</c>.
    /// </summary>
    private static bool HasForwardingRegistration(SyntaxNode scope, string concreteTypeName)
    {
        var target = "GetRequiredService<" + concreteTypeName + ">";
        return ScopeContainsText(scope, target);
    }

    /// <summary>
    /// Returns true if the scope contains <c>AddHttpClient&lt;ConcreteType&gt;()</c>.
    /// </summary>
    private static bool HasTypedHttpClientRegistration(SyntaxNode scope, string concreteTypeName)
    {
        var target = "AddHttpClient<" + concreteTypeName + ">";
        return ScopeContainsText(scope, target);
    }

    private static bool ScopeContainsText(SyntaxNode scope, string target)
    {
        // For BlockSyntax, scan statements; for CompilationUnit (top-level), scan the full text.
        var text = scope.ToString();
        return text.IndexOf(target, StringComparison.Ordinal) >= 0;
    }

    private static bool InheritsFromDelegate(ITypeSymbol type)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (string.Equals(current.ToDisplayString(), "System.Delegate", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
