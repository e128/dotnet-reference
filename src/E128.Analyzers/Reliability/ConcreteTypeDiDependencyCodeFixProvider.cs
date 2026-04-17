using System;
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

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128035: replaces <c>AddSingleton&lt;IFoo, TImpl&gt;()</c> with
///     <c>AddSingleton&lt;TImpl&gt;()</c> + <c>AddSingleton&lt;IFoo&gt;(sp =&gt; sp.GetRequiredService&lt;TImpl&gt;())</c>
///     .
///     This ensures the concrete type is directly resolvable from the DI container.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConcreteTypeDiDependencyCodeFixProvider))]
[Shared]
public sealed class ConcreteTypeDiDependencyCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ConcreteTypeDiDependencyAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];

        var concreteTypeName = diagnostic.Properties.GetValueOrDefault("ConcreteType");
        if (concreteTypeName is null)
        {
            return;
        }

        var registrationInfo = FindInterfaceMappedRegistration(root, concreteTypeName);
        if (registrationInfo is null)
        {
            return;
        }

        var title = $"Add direct registration for {concreteTypeName}";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                ct => RewriteToDirectAndForwardingAsync(
                    context.Document, registrationInfo.Value.Invocation,
                    registrationInfo.Value.Lifetime, concreteTypeName,
                    registrationInfo.Value.InterfaceName, ct),
                $"E128035_{concreteTypeName}"),
            diagnostic);
    }

    private static (InvocationExpressionSyntax Invocation, string Lifetime, string InterfaceName)?
        FindInterfaceMappedRegistration(SyntaxNode root, string concreteTypeName)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (memberAccess.Name is not GenericNameSyntax genericName)
            {
                continue;
            }

            var methodName = genericName.Identifier.Text;
            if (methodName is not ("AddSingleton" or "AddScoped" or "AddTransient"))
            {
                continue;
            }

            if (genericName.TypeArgumentList.Arguments.Count != 2)
            {
                continue;
            }

            var implArg = genericName.TypeArgumentList.Arguments[1].ToString();
            if (string.Equals(implArg, concreteTypeName, StringComparison.Ordinal))
            {
                var interfaceName = genericName.TypeArgumentList.Arguments[0].ToString();
                var lifetime = methodName.Substring(3);
                return (invocation, lifetime, interfaceName);
            }
        }

        return null;
    }

    private static async Task<Document> RewriteToDirectAndForwardingAsync(
        Document document,
        InvocationExpressionSyntax originalInvocation,
        string lifetime,
        string concreteTypeName,
        string interfaceName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (originalInvocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        var receiver = memberAccess.Expression.ToString();

        var directText = $"{receiver}.Add{lifetime}<{concreteTypeName}>();";
        var directRegistration = SyntaxFactory.ParseStatement(directText);
        if (directRegistration.ContainsDiagnostics)
        {
            return document;
        }

        var forwardingText = $"{receiver}.Add{lifetime}<{interfaceName}>(sp => sp.GetRequiredService<{concreteTypeName}>());";
        var forwardingRegistration = SyntaxFactory.ParseStatement(forwardingText);
        if (forwardingRegistration.ContainsDiagnostics)
        {
            return document;
        }

        var containingStatement = originalInvocation.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (containingStatement is null)
        {
            return document;
        }

        var leadingTrivia = containingStatement.GetLeadingTrivia();
        var trailingTrivia = containingStatement.GetTrailingTrivia();

        directRegistration = directRegistration
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(trailingTrivia);
        forwardingRegistration = forwardingRegistration
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(trailingTrivia);

        var newStatements = new SyntaxNode[]
        {
            directRegistration,
            forwardingRegistration
        };

        var newRoot = root.ReplaceNode(containingStatement, newStatements);
        return document.WithSyntaxRoot(newRoot);
    }
}
