using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128189: A <c lang="csharp">GCHandle.Alloc</c> result stored in a local must be freed with
///     <c lang="csharp">Free</c> in the
///     same method. The rule stays silent when the local can leave the method through a call, a
///     return, a field, or a collection.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GcHandleFreeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128189";

    private const string GcHandleMetadataName = "System.Runtime.InteropServices.GCHandle";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "GCHandle with no Free call",
        "The handle from '{0}' is never freed, so the pinned object stays reachable and pinned",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var gcHandleType = startContext.Compilation.GetTypeByMetadataName(GcHandleMetadataName);
            if (gcHandleType is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, gcHandleType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol gcHandleType)
    {
        if (context.Node is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || !string.Equals(access.Name.Identifier.ValueText, "Alloc", StringComparison.Ordinal))
        {
            return;
        }

        var model = context.SemanticModel;
        var token = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, gcHandleType))
        {
            return;
        }

        if (!TryGetAssignedLocal(invocation, model, token, out var local))
        {
            return;
        }

        var body = FindEnclosingBody(invocation);
        if (body is null)
        {
            return;
        }

        var uses = body.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(name => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, token).Symbol, local));

        foreach (var use in uses)
        {
            if (Frees(use, gcHandleType, model, token))
            {
                return;
            }

            if (Escapes(use))
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), invocation.ToString()));
    }

    // A local declaration such as "var handle = GCHandle.Alloc(x)" or a later assignment such as
    // "handle = GCHandle.Alloc(x)". A field, property, parameter, or element target is not a local,
    // so the rule stays silent for it.
    private static bool TryGetAssignedLocal(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        System.Threading.CancellationToken token,
        [NotNullWhen(true)] out ILocalSymbol? local)
    {
        local = null;

        if (invocation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            local = model.GetDeclaredSymbol(declarator, token) as ILocalSymbol;
            return local is not null;
        }

        if (invocation.Parent is AssignmentExpressionSyntax assignment
            && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            local = model.GetSymbolInfo(assignment.Left, token).Symbol as ILocalSymbol;
            return local is not null;
        }

        return false;
    }

    private static bool Frees(
        IdentifierNameSyntax receiver,
        INamedTypeSymbol gcHandleType,
        SemanticModel model,
        System.Threading.CancellationToken token)
    {
        return receiver.Parent is MemberAccessExpressionSyntax access
               && string.Equals(access.Name.Identifier.ValueText, "Free", StringComparison.Ordinal)
               && access.Parent is InvocationExpressionSyntax freeCall
               && model.GetSymbolInfo(freeCall, token).Symbol is IMethodSymbol method
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, gcHandleType);
    }

    // Any use that lets the handle leave the method body makes the handle unreachable to this
    // analysis. Reading the handle in place, such as Target or IsAllocated, does not.
    private static bool Escapes(IdentifierNameSyntax identifier)
    {
        var parent = identifier.Parent;

        return parent is ArgumentSyntax or ReturnStatementSyntax or ArrowExpressionClauseSyntax
                   or EqualsValueClauseSyntax or InitializerExpressionSyntax
               || (parent is AssignmentExpressionSyntax assignment && !ReferenceEquals(assignment.Left, identifier));
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
