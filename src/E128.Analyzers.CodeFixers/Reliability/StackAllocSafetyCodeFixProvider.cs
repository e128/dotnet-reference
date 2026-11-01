using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StackAllocSafetyCodeFixProvider))]
[Shared]
public sealed class StackAllocSafetyCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [StackAllocSafetyAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not StackAllocArrayCreationExpressionSyntax stackAlloc
            || stackAlloc.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable }
            || variable.Parent is not VariableDeclarationSyntax { Type: PointerTypeSyntax pointerType, Variables.Count: 1 }
            || IsInsideLoop(stackAlloc))
        {
            return Task.CompletedTask;
        }

        if (!IsIndexOnly(variable.Identifier.ValueText, stackAlloc))
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Assign the buffer to a Span",
                _ => Task.FromResult(ApplyFix(context.Document, root, pointerType)),
                nameof(StackAllocSafetyCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        PointerTypeSyntax pointerType)
    {
        var spanType = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("Span"),
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList(pointerType.ElementType)));

        var newRoot = root.ReplaceNode(pointerType, spanType.WithTriviaFrom(pointerType));
        return document.WithSyntaxRoot(UsingDirectives.AddIfMissing(newRoot, "System"));
    }

    // In a loop the pointer is not the problem, so a Span would leave the diagnostic in place.
    private static bool IsInsideLoop(SyntaxNode node)
    {
        return node.Ancestors().TakeWhile(ancestor => ancestor is not MemberDeclarationSyntax)
            .Any(ancestor => ancestor is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax);
    }

    private static bool IsIndexOnly(string variableName, SyntaxNode stackAlloc)
    {
        var scope = stackAlloc
            .Ancestors()
            .FirstOrDefault(ancestor => ancestor is BlockSyntax or ArrowExpressionClauseSyntax);

        if (scope is null)
        {
            return false;
        }

        foreach (var identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!string.Equals(identifier.Identifier.ValueText, variableName, StringComparison.Ordinal))
            {
                continue;
            }

            if (identifier.Parent is not ElementAccessExpressionSyntax elementAccess
                || elementAccess.Expression != identifier
                || elementAccess.Parent is PrefixUnaryExpressionSyntax prefix && prefix.IsKind(SyntaxKind.AddressOfExpression))
            {
                return false;
            }
        }

        return true;
    }
}
