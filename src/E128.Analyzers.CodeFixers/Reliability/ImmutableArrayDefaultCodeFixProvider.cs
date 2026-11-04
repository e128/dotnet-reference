using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128163: initializes the field to
///     <c lang="csharp">ImmutableArray&lt;T&gt;.Empty</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ImmutableArrayDefaultCodeFixProvider))]
[Shared]
public sealed class ImmutableArrayDefaultCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ImmutableArrayDefaultAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var variable = node.FirstAncestorOrSelf<VariableDeclaratorSyntax>();

        if (variable?.Parent is not VariableDeclarationSyntax declaration)
        {
            return Task.CompletedTask;
        }

        // A struct instance field cannot have an initializer on older language versions.
        // Register the fix only for a static field or a field in a non-struct type.
        if (declaration.Parent is FieldDeclarationSyntax field
            && field.Parent is TypeDeclarationSyntax owner
            && owner.Kind() is SyntaxKind.StructDeclaration or SyntaxKind.RecordStructDeclaration
            && !field.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return Task.CompletedTask;
        }

        var typeText = declaration.Type.ToString();

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Initialize to {typeText}.Empty",
                ct => InitializeAsync(context.Document, variable, typeText, ct),
                nameof(ImmutableArrayDefaultCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> InitializeAsync(
        Document document,
        VariableDeclaratorSyntax variable,
        string typeText,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        var initializer = SyntaxFactory.EqualsValueClause(
            SyntaxFactory.Token(SyntaxKind.EqualsToken).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.ParseExpression($"{typeText}.Empty"));

        var newVariable = variable.WithInitializer(initializer)
            .WithIdentifier(variable.Identifier.WithTrailingTrivia(SyntaxFactory.Space));

        return document.WithSyntaxRoot(root.ReplaceNode(variable, newVariable));
    }
}
