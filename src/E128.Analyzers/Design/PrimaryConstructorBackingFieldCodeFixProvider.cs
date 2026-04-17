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

        var newRoot = ReplaceFieldReferences(root, fieldSymbol, parameterName, semanticModel, cancellationToken);
        newRoot = RemoveFieldDeclaration(newRoot, variable);

        return document.WithSyntaxRoot(newRoot);
    }

    private static string? GetParameterName(VariableDeclaratorSyntax variable)
    {
        return variable.Initializer?.Value is IdentifierNameSyntax identifier
            ? identifier.Identifier.ValueText
            : null;
    }

    private static SyntaxNode ReplaceFieldReferences(
        SyntaxNode root,
        IFieldSymbol fieldSymbol,
        string parameterName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var annotation = new SyntaxAnnotation("FieldReference");
        var nodesToAnnotate = CollectFieldReferences(root, fieldSymbol, semanticModel, cancellationToken);

        if (nodesToAnnotate.Count == 0)
        {
            return root;
        }

        root = root.ReplaceNodes(nodesToAnnotate, (_, rewritten) =>
            rewritten.WithAdditionalAnnotations(annotation));

        var annotated = root.GetAnnotatedNodes(annotation).ToArray();
        return root.ReplaceNodes(annotated, (_, rewritten) =>
            SyntaxFactory.IdentifierName(parameterName).WithTriviaFrom(rewritten));
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

            if (IsFieldInitializer(id))
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

    private static bool IsFieldInitializer(IdentifierNameSyntax identifier)
    {
        return identifier.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax };
    }

    private static SyntaxNode RemoveFieldDeclaration(
        SyntaxNode root,
        VariableDeclaratorSyntax variable)
    {
        var currentField = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables
                .Any(v => string.Equals(v.Identifier.ValueText, variable.Identifier.ValueText, StringComparison.Ordinal)));

        return currentField is null
            ? root
            : currentField.Declaration.Variables.Count == 1
                ? root.RemoveNode(currentField, SyntaxRemoveOptions.KeepNoTrivia)!
                : root.RemoveNode(
                    currentField.Declaration.Variables.First(v =>
                        string.Equals(v.Identifier.ValueText, variable.Identifier.ValueText, StringComparison.Ordinal)),
                    SyntaxRemoveOptions.KeepNoTrivia)!;
    }
}
