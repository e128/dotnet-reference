using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
/// Code fix for E128032: rewrites <c>Add{Lifetime}&lt;TImpl&gt;()</c> to
/// <c>Add{Lifetime}&lt;IFoo, TImpl&gt;()</c>. When the concrete type implements
/// multiple non-marker interfaces, one <see cref="CodeAction"/> is offered per interface.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConcreteOnlyDiRegistrationCodeFixProvider))]
[Shared]
public sealed class ConcreteOnlyDiRegistrationCodeFixProvider : CodeFixProvider
{
    private static readonly HashSet<string> MarkerInterfaces = new(StringComparer.Ordinal)
    {
        "System.IDisposable",
        "System.IAsyncDisposable",
    };

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ConcreteOnlyDiRegistrationAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

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

        if (semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (methodSymbol.TypeArguments.Length != 1)
        {
            return;
        }

        var concreteType = methodSymbol.TypeArguments[0];
        var nonMarkerInterfaces = concreteType.AllInterfaces
            .Where(i => !MarkerInterfaces.Contains(i.ToDisplayString()))
            .ToList();

        foreach (var iface in nonMarkerInterfaces)
        {
            var title = $"Register as Add{methodSymbol.Name.Substring(3)}<{iface.Name}, {concreteType.Name}>()";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    ct => RewriteToInterfaceMappedAsync(context.Document, invocation, concreteType, iface, ct),
                    equivalenceKey: $"E128032_{iface.ToDisplayString()}"),
                diagnostic);
        }
    }

    private static async Task<Document> RewriteToInterfaceMappedAsync(
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

        var newTypeArgs = SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SeparatedList(
            [
                SyntaxFactory.ParseTypeName(targetInterface.Name),
                SyntaxFactory.ParseTypeName(concreteType.Name),
            ]));

        var newName = SyntaxFactory.GenericName(memberAccess.Name.Identifier)
            .WithTypeArgumentList(newTypeArgs);

        var newMemberAccess = memberAccess.WithName(newName);
        var newInvocation = invocation.WithExpression(newMemberAccess);

        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
