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

        var rootAndModel = await FixSyntax.GetRootAndSemanticModelAsync(context.Document, context.CancellationToken).ConfigureAwait(false);
        if (rootAndModel is null)
        {
            return;
        }

        var semanticModel = rootAndModel.Value.SemanticModel;

        var match = ReadonlyFieldLoopHoistAnalyzer.TryMatchHoist(loop, semanticModel, context.CancellationToken);
        if (match is null)
        {
            return;
        }

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
        var targets = new List<SyntaxNode>();

        foreach (var identifier in loop.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    field))
            {
                continue;
            }

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
        var typeName = SyntaxFactory.ParseTypeName(
            field.Type.ToMinimalDisplayString(semanticModel, loop.SpanStart));

        var declarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(localName))
            .WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.IdentifierName(field.Name)));

        return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(typeName)
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(declarator)))
            .WithLeadingTrivia(FixSyntax.AnchorLeadingTrivia(loop))
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
}
