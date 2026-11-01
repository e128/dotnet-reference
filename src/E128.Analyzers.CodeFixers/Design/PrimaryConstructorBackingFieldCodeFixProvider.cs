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
using Microsoft.CodeAnalysis.Editing;

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
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var root = editor.OriginalRoot;
        var semanticModel = editor.SemanticModel;

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

        // Two types in one file can declare the same field name. The tracked nodes from the original
        // tree, not a text search, select this exact declaration and its references.
        foreach (var reference in CollectFieldReferences(root, fieldSymbol, semanticModel, cancellationToken))
        {
            editor.ReplaceNode(
                reference,
                SyntaxFactory.IdentifierName(parameterName).WithTriviaFrom(reference));
        }

        editor.RemoveNode(
            fieldDeclaration.Declaration.Variables.Count == 1 ? fieldDeclaration : variable,
            SyntaxRemoveOptions.KeepNoTrivia);

        return editor.GetChangedDocument();
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
}
