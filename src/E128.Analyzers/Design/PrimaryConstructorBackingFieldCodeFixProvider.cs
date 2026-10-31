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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PrimaryConstructorBackingFieldCodeFixProvider))]
[Shared]
public sealed class PrimaryConstructorBackingFieldCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [PrimaryConstructorBackingFieldAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
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
        var node = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent;

        if (node is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove backing field, use primary constructor parameter",
                ct => ApplyFixAsync(context.Document, diagnostic, ct),
                nameof(PrimaryConstructorBackingFieldCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        var variable = token.Parent?.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
        if (variable is null)
        {
            return document;
        }

        var parameterName = GetParameterName(variable);
        if (parameterName is null)
        {
            return document;
        }

        if (semanticModel.GetDeclaredSymbol(variable, cancellationToken) is not IFieldSymbol fieldSymbol)
        {
            return document;
        }

        var fieldDeclaration = variable.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (fieldDeclaration is null)
        {
            return document;
        }

        // Collect the references from the original tree, because the semantic model is bound to it.
        var references = CollectFieldReferences(root, fieldSymbol, semanticModel, cancellationToken);

        var newRoot = RewriteTree(
            root, fieldDeclaration, references, parameterName, variable.Identifier.ValueText);

        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode RewriteTree(
        SyntaxNode root,
        FieldDeclarationSyntax fieldDeclaration,
        List<SyntaxNode> references,
        string parameterName,
        string fieldName)
    {
        // Annotate the declaration and the references in one pass over the original tree. Two types
        // in one file can declare the same field name, so the removal must find this exact
        // declaration, not the first text match.
        var declarationAnnotation = new SyntaxAnnotation("E128BackingField");
        var referenceAnnotation = new SyntaxAnnotation("FieldReference");
        var targets = new List<SyntaxNode>(references) { fieldDeclaration };
        var annotatedRoot = root.ReplaceNodes(targets, (original, _) =>
            ReferenceEquals(original, fieldDeclaration)
                ? original.WithAdditionalAnnotations(declarationAnnotation)
                : original.WithAdditionalAnnotations(referenceAnnotation));

        var newRoot = annotatedRoot.ReplaceNodes(
            annotatedRoot.GetAnnotatedNodes(referenceAnnotation),
            (_, rewritten) => SyntaxFactory.IdentifierName(parameterName).WithTriviaFrom(rewritten));

        var target = newRoot.GetAnnotatedNodes(declarationAnnotation)
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault();

        return target is null
            ? newRoot
            : RemoveFieldDeclaration(newRoot, target, fieldName);
    }

    private static string? GetParameterName(VariableDeclaratorSyntax variable)
    {
        return variable.Initializer?.Value is IdentifierNameSyntax identifier
            ? identifier.Identifier.ValueText
            : null;
    }

    private static List<SyntaxNode> CollectFieldReferences(
        SyntaxNode root,
        IFieldSymbol fieldSymbol,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var nodes = new List<SyntaxNode>();

        foreach (var id in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!string.Equals(id.Identifier.ValueText, fieldSymbol.Name, StringComparison.Ordinal))
            {
                continue;
            }

            var symbol = semanticModel.GetSymbolInfo(id, cancellationToken).Symbol;
            if (symbol is not IFieldSymbol referencedField
                || !SymbolEqualityComparer.Default.Equals(referencedField, fieldSymbol))
            {
                continue;
            }

            if (id.Parent is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Expression is ThisExpressionSyntax)
            {
                nodes.Add(memberAccess);
            }
            else
            {
                nodes.Add(id);
            }
        }

        return nodes;
    }

    private static SyntaxNode RemoveFieldDeclaration(
        SyntaxNode root,
        FieldDeclarationSyntax fieldDeclaration,
        string identifier)
    {
        if (fieldDeclaration.Declaration.Variables.Count == 1)
        {
            return root.RemoveNode(fieldDeclaration, SyntaxRemoveOptions.KeepNoTrivia) ?? root;
        }

        var target = fieldDeclaration.Declaration.Variables
            .FirstOrDefault(v => string.Equals(v.Identifier.ValueText, identifier, StringComparison.Ordinal));

        return target is null
            ? root
            : root.RemoveNode(target, SyntaxRemoveOptions.KeepNoTrivia) ?? root;
    }
}
