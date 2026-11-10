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
///     <c lang="csharp">Free</c> in the same member body. The rule stays silent when the local can
///     leave that body through a call, a return, a field, or a collection.
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

        context.RegisterCompilationStartAction(static startContext =>
            MetadataTypes.RegisterForType(
                startContext,
                GcHandleMetadataName,
                AnalyzeInvocation,
                SyntaxKind.InvocationExpression));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol gcHandleType)
    {
        // The bare form "Alloc(x)" binds through "using static System.Runtime.InteropServices.GCHandle".
        if (context.Node is not InvocationExpressionSyntax invocation
            || !IsAllocName(invocation.Expression))
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

        var body = FindOutermostMemberBody(invocation);
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

    // A lambda or local function body is too small a scope. The handle outlives that body, so the
    // free can sit anywhere in the outermost member body.
    private static BlockSyntax? FindOutermostMemberBody(SyntaxNode node)
    {
        BlockSyntax? body = null;

        foreach (var ancestor in node.Ancestors())
        {
            var candidate = ancestor switch
            {
                MethodDeclarationSyntax method => method.Body,
                ConstructorDeclarationSyntax constructor => constructor.Body,
                DestructorDeclarationSyntax destructor => destructor.Body,
                OperatorDeclarationSyntax operatorDeclaration => operatorDeclaration.Body,
                ConversionOperatorDeclarationSyntax conversion => conversion.Body,
                AccessorDeclarationSyntax accessor => accessor.Body,
                _ => null
            };

            if (candidate is not null)
            {
                body = candidate;
            }
        }

        return body;
    }

    private static bool IsAllocName(ExpressionSyntax expression)
    {
        var name = expression switch
        {
            MemberAccessExpressionSyntax access => access.Name,
            IdentifierNameSyntax identifier => identifier,
            _ => null
        };

        return name is not null && string.Equals(name.Identifier.ValueText, "Alloc", StringComparison.Ordinal);
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

        if (invocation.Parent is not AssignmentExpressionSyntax assignment
            || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            return false;
        }

        local = model.GetSymbolInfo(assignment.Left, token).Symbol as ILocalSymbol;
        return local is not null;
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
}
