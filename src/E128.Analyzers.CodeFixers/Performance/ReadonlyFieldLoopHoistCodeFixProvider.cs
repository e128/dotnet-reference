using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128235: declares a local before the reported loop, typed from the
///     field, and rewrites the field references inside that one loop statement to read
///     the local.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReadonlyFieldLoopHoistCodeFixProvider))]
[Shared]
public sealed class ReadonlyFieldLoopHoistCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ReadonlyFieldLoopHoistAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not StatementSyntax loop)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        // The analyzer owns the shape check, so the fix repairs exactly what the rule reports.
        var match = ReadonlyFieldLoopHoistAnalyzer.TryMatchHoist(loop, semanticModel, context.CancellationToken);
        if (match is null)
        {
            return;
        }

        // Build the replacement here, so a registered action never discovers a reason to stop.
        var newRoot = TryBuildReplacement(
            root,
            loop,
            match.Value.Field,
            match.Value.LocalName,
            semanticModel,
            context.CancellationToken);
        if (newRoot is null)
        {
            return;
        }

        CodeFixRegistration.Register(
            context,
            diagnostic,
            "Hoist '" + match.Value.Field.Name + "' into a local",
            nameof(ReadonlyFieldLoopHoistCodeFixProvider),
            _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)));
    }

    private static SyntaxNode? TryBuildReplacement(
        SyntaxNode root,
        StatementSyntax loop,
        IFieldSymbol field,
        string localName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (loop.Parent is not BlockSyntax block)
        {
            return null;
        }

        var targets = CollectReferences(loop, field, semanticModel, cancellationToken);
        if (targets.Count is 0)
        {
            return null;
        }

        // One pass over the original tree keeps every rewrite on one tree identity.
        var newLoop = loop.ReplaceNodes(
            targets,
            (original, _) => SyntaxFactory.IdentifierName(localName).WithTriviaFrom(original));

        var declaration = CreateLocalDeclaration(field, localName, loop, semanticModel);
        var newBlock = InsertDeclarationBefore(block, loop, declaration, newLoop);

        return root.ReplaceNode(block, newBlock);
    }

    private static List<SyntaxNode> CollectReferences(
        StatementSyntax loop,
        IFieldSymbol field,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        // Each identifier node adds exactly one target, so a plain list needs no lookup and no
        // custom equality for a type that compares by reference.
        var targets = new List<SyntaxNode>();

        foreach (var identifier in loop.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    field))
            {
                continue;
            }

            // Only the name of a member access replaces the whole access, so `this._items` and
            // `C._items` rewrite as one node. A receiver identifier, as in `_items.Length`,
            // rewrites itself and keeps the member access intact.
            if (identifier.Parent is MemberAccessExpressionSyntax { Name: { } } access && access.Name == identifier)
            {
                targets.Add(access);
            }
            else
            {
                targets.Add(identifier);
            }
        }

        return targets;
    }

    private static LocalDeclarationStatementSyntax CreateLocalDeclaration(
        IFieldSymbol field,
        string localName,
        StatementSyntax loop,
        SemanticModel semanticModel)
    {
        // The field type, emitted through the minimal display string, keeps the type arguments
        // and qualifies the name only when it is out of scope.
        var typeName = SyntaxFactory.ParseTypeName(
            field.Type.ToMinimalDisplayString(semanticModel, loop.SpanStart));

        var declarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(localName))
            .WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.IdentifierName(field.Name)));

        return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(typeName)
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(declarator)))
            .WithLeadingTrivia(IndentationTrivia(loop))
            .WithTrailingTrivia(SyntaxFactory.LineFeed)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static BlockSyntax InsertDeclarationBefore(
        BlockSyntax block,
        StatementSyntax loop,
        StatementSyntax declaration,
        StatementSyntax newLoop)
    {
        var statements = new List<StatementSyntax>(block.Statements.Count + 1);
        foreach (var statement in block.Statements)
        {
            if (statement == loop)
            {
                statements.Add(declaration);
                statements.Add(newLoop);
            }
            else
            {
                statements.Add(statement);
            }
        }

        return block.WithStatements(SyntaxFactory.List(statements));
    }

    /// <summary>
    ///     Copies only the line break and the indentation of the anchor line. Copying the whole
    ///     leading list would repeat a comment that sits above the anchor on the inserted line.
    /// </summary>
    private static SyntaxTriviaList IndentationTrivia(SyntaxNode anchor)
    {
        var indent = SyntaxFactory.Whitespace(FixSyntax.TrailingWhitespace(anchor.GetLeadingTrivia()).ToString());
        var previous = anchor.GetFirstToken().GetPreviousToken();

        return !previous.IsKind(SyntaxKind.None) && !previous.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia)
            ? [SyntaxFactory.LineFeed, indent]
            : [indent];
    }
}
