using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128031: rewrites <c lang="csharp">AddSingleton(sp => new T())</c> to
///     <c lang="csharp">AddSingleton&lt;IFoo, T&gt;()</c> when the concrete type implements a non-marker interface.
///     Offers one code action per non-marker interface the type implements.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DisposableSingletonFactoryCodeFixProvider))]
[Shared]
public sealed class DisposableSingletonFactoryCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DisposableSingletonFactoryAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var returnType = ResolveReturnType(semanticModel, invocation, context.CancellationToken);
        if (returnType is null)
        {
            return;
        }

        // The two-type-argument overload asks the container to construct the type itself. A factory
        // that passes any argument builds something the container cannot reproduce.
        if (!FixSyntax.IsParameterlessCreation(invocation.ArgumentList.Arguments[0].Expression))
        {
            return;
        }

        var nonMarkerInterfaces = returnType.AllInterfaces
            .Where(i => !ConcreteOnlyDiRegistrationAnalyzer.MarkerInterfaces.Contains(i.ToDisplayString()))
            .ToList();

        foreach (var iface in nonMarkerInterfaces)
        {
            // The minimal form keeps the type arguments and matches the text the fix writes.
            var title = $"Register as AddSingleton<{iface.ToMinimalDisplayString(semanticModel, invocation.SpanStart)}, {returnType.ToMinimalDisplayString(semanticModel, invocation.SpanStart)}>()";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    ct => RewriteToGenericOverloadAsync(context.Document, invocation, returnType, iface, ct),
                    $"E128031_{iface.ToDisplayString()}"),
                diagnostic);
        }
    }

    private static ITypeSymbol? ResolveReturnType(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var arg = invocation.ArgumentList.Arguments[0].Expression;
        var bodyExpr = arg switch
        {
            SimpleLambdaExpressionSyntax s => s.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax p => p.Body as ExpressionSyntax,
            _ => null
        };

        return bodyExpr is null ? null : semanticModel.GetTypeInfo(bodyExpr, cancellationToken).Type;
    }

    private static async Task<Document> RewriteToGenericOverloadAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ITypeSymbol concreteType,
        INamedTypeSymbol targetInterface,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        // A bare Name drops the type arguments and the namespace. `Repository` alone, for
        // Repository<Order>, does not compile, so the written name carries both.
        var newTypeArgs = SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SeparatedList(
                [
                    SyntaxFactory.ParseTypeName(
                        targetInterface.ToMinimalDisplayString(semanticModel, invocation.SpanStart)),
                    SyntaxFactory.ParseTypeName(
                        concreteType.ToMinimalDisplayString(semanticModel, invocation.SpanStart))
                ]));

        var newName = SyntaxFactory.GenericName(
                SyntaxFactory.Identifier("AddSingleton"))
            .WithTypeArgumentList(newTypeArgs);

        var newMemberAccess = memberAccess.WithName(newName);
        var newInvocation = invocation
            .WithExpression(newMemberAccess)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
