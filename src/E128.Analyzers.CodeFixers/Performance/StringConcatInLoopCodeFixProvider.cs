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

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StringConcatInLoopCodeFixProvider))]
[Shared]
public sealed class StringConcatInLoopCodeFixProvider : E128CodeFixProviderBase
{
    private static readonly string[] BuilderNames = ["sb", "stringBuilder", "builder", "accumulator"];

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [StringConcatInLoopAnalyzer.DiagnosticId];

    // Two loops in one method would each pick the same builder name, so a batch merge would emit two
    // declarations of it. Keep the fix single-diagnostic.
    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var assignment = root.FindNode(diagnostic.Location.SourceSpan)
                .FirstAncestorOrSelf<AssignmentExpressionSyntax>();
            if (assignment is null || !assignment.IsKind(SyntaxKind.AddAssignmentExpression))
            {
                continue;
            }

            var newRoot = TryRewrite(root, assignment, semanticModel, context.CancellationToken);
            if (newRoot is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use a StringBuilder",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)),
                    nameof(StringConcatInLoopCodeFixProvider)),
                diagnostic);
        }
    }

    // The rewrite moves the accumulation into a builder declared before the loop and reads the text
    // back after it. Every shape that would change behavior declines instead of guessing.
    private static SyntaxNode? TryRewrite(
        SyntaxNode root,
        AssignmentExpressionSyntax assignment,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (assignment.Parent is not ExpressionStatementSyntax statement
            || assignment.Left is not IdentifierNameSyntax target
            || semanticModel.GetSymbolInfo(target, cancellationToken).Symbol is not ILocalSymbol local)
        {
            return null;
        }

        var loop = FindLoop(statement);
        if (loop is null
            || loop.Parent is not BlockSyntax block
            || !IsEmptyDeclarationBeforeLoop(block, loop, local, semanticModel, cancellationToken)
            || IsUnsafeToRewrite(loop, local, semanticModel, cancellationToken))
        {
            return null;
        }

        if (PickBuilderName(loop, semanticModel) is not { } builderName)
        {
            return null;
        }

        var newLoop = ReplaceEveryAppend(loop, local, builderName, semanticModel, cancellationToken);
        var indentation = GetIndentation(loop);
        var statements = new List<StatementSyntax>(block.Statements.Count + 2);

        foreach (var child in block.Statements)
        {
            if (child != loop)
            {
                statements.Add(child);
                continue;
            }

            statements.Add(CreateBuilderDeclaration(builderName, indentation));
            statements.Add(newLoop);
            statements.Add(CreateReadBack(target.WithoutTrivia(), builderName, indentation));
        }

        var newRoot = root.ReplaceNode(block, block.WithStatements(SyntaxFactory.List(statements)));
        return UsingDirectives.AddIfMissing(newRoot, "System.Text");
    }

    // Every concatenation on the local becomes an append, not only the reported one. A second fix
    // pass would otherwise declare a second builder for the same variable.
    private static StatementSyntax ReplaceEveryAppend(
        StatementSyntax loop,
        ILocalSymbol local,
        string builderName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var concatenations = new List<AssignmentExpressionSyntax>();

        foreach (var candidate in loop.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (candidate.IsKind(SyntaxKind.AddAssignmentExpression)
                && candidate.Left is IdentifierNameSyntax left
                && SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(left, cancellationToken).Symbol, local))
            {
                concatenations.Add(candidate);
            }
        }

        return loop.ReplaceNodes(
            concatenations,
            (original, _) => SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(builderName),
                        SyntaxFactory.IdentifierName("Append")),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(original.Right))))
                .WithTriviaFrom(original));
    }

    // A local declared outside the block that holds the loop, a seeded value, or a second use of the
    // variable inside the loop all make the builder rewrite change behavior.
    private static bool IsUnsafeToRewrite(
        StatementSyntax loop,
        ILocalSymbol local,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var node in loop.DescendantNodes())
        {
            if (node is ReturnStatementSyntax or ThrowStatementSyntax or YieldStatementSyntax or GotoStatementSyntax)
            {
                return true;
            }

            if (node is IdentifierNameSyntax identifier
                && SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol, local)
                && !IsAddAssignmentLeft(identifier))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAddAssignmentLeft(IdentifierNameSyntax identifier)
    {
        return identifier.Parent is AssignmentExpressionSyntax assignment
               && assignment.Left == identifier
               && assignment.IsKind(SyntaxKind.AddAssignmentExpression);
    }

    private static bool IsEmptyDeclarationBeforeLoop(
        BlockSyntax block,
        StatementSyntax loop,
        ILocalSymbol local,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var statement in block.Statements)
        {
            if (statement == loop)
            {
                return false;
            }

            if (statement is not LocalDeclarationStatementSyntax { Declaration.Variables.Count: 1 } declaration)
            {
                continue;
            }

            var variable = declaration.Declaration.Variables[0];
            if (variable.Initializer?.Value is not { } value
                || !SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetDeclaredSymbol(variable, cancellationToken), local))
            {
                continue;
            }

            return IsEmptyString(value, semanticModel, cancellationToken);
        }

        return false;
    }

    private static bool IsEmptyString(
        ExpressionSyntax value,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return value switch
        {
            LiteralExpressionSyntax => semanticModel.GetConstantValue(value, cancellationToken) is { HasValue: true, Value: "" },
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Empty" } member =>
                semanticModel.GetSymbolInfo(member, cancellationToken).Symbol
                    is IPropertySymbol { ContainingType.SpecialType: SpecialType.System_String },
            _ => false
        };
    }

    private static StatementSyntax? FindLoop(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is ForEachStatementSyntax or ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return (StatementSyntax)ancestor;
            }

            // A lambda can outlive the loop, so a captured builder would be read before the appends run.
            if (ancestor is AnonymousFunctionExpressionSyntax or MemberDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                return null;
            }
        }

        return null;
    }

    private static string? PickBuilderName(StatementSyntax loop, SemanticModel semanticModel)
    {
        var taken = CollectNames(loop);

        foreach (var candidate in BuilderNames)
        {
            if (!taken.Contains(candidate)
                && semanticModel.LookupSymbols(loop.SpanStart, name: candidate).IsEmpty)
            {
                return candidate;
            }
        }

        return null;
    }

    // A name declared later in the method, or inside the loop, still conflicts with a new local in an
    // enclosing scope, so collect every local and parameter name the member declares.
    private static HashSet<string> CollectNames(SyntaxNode loop)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);

        foreach (var ancestor in loop.AncestorsAndSelf())
        {
            if (ancestor is not (MemberDeclarationSyntax or LocalFunctionStatementSyntax))
            {
                continue;
            }

            foreach (var node in ancestor.DescendantNodes())
            {
                // A lambda parameter is a ParameterSyntax, so it needs no case of its own.
                if (node is VariableDeclaratorSyntax declarator)
                {
                    taken.Add(declarator.Identifier.ValueText);
                }
                else if (node is ParameterSyntax parameter)
                {
                    taken.Add(parameter.Identifier.ValueText);
                }
                else if (node is LocalFunctionStatementSyntax function)
                {
                    taken.Add(function.Identifier.ValueText);
                }
            }

            break;
        }

        return taken;
    }

    private static LocalDeclarationStatementSyntax CreateBuilderDeclaration(
        string builderName,
        SyntaxTriviaList indentation)
    {
        return SyntaxFactory
            .LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                    .WithVariables(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(builderName))
                                .WithInitializer(
                                    SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.ObjectCreationExpression(
                                                SyntaxFactory.IdentifierName("StringBuilder"))
                                            .WithArgumentList(SyntaxFactory.ArgumentList()))))))
            .WithLeadingTrivia(indentation)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }

    private static ExpressionStatementSyntax CreateReadBack(
        IdentifierNameSyntax target,
        string builderName,
        SyntaxTriviaList indentation)
    {
        return SyntaxFactory
            .ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    target,
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(builderName),
                            SyntaxFactory.IdentifierName("ToString")))))
            .WithLeadingTrivia(indentation)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }

    private static SyntaxTriviaList GetIndentation(SyntaxNode node)
    {
        var leading = node.GetLeadingTrivia();
        return leading.Count > 0 && leading.Last().IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(leading.Last())
            : SyntaxFactory.TriviaList();
    }
}
