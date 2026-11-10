using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using MembershipScan = (Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax Guard, Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax OuterSide, string CollectionName, string SetName);

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128152: replaces a two-deep scan whose only job is one equality test with a
///     set lookup. The inner loop disappears, so the reported nesting depth drops to one.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ComplexityCeilingMembershipScanCodeFixProvider))]
[Shared]
public sealed class ComplexityCeilingMembershipScanCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ComplexityCeilingAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (node is not ForEachStatementSyntax inner
            || inner.Parent is not BlockSyntax { Parent: ForEachStatementSyntax outer }
            || outer.Parent is not (BlockSyntax or SwitchSectionSyntax or GlobalStatementSyntax)
            || IsInsideLoop(outer))
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return;
        }

        var scan = Recognize(inner, outer, model, context.CancellationToken);
        if (scan is null)
        {
            return;
        }

        CodeFixRegistration.Register(context, diagnostic, "Replace the nested scan with a set lookup", nameof(ComplexityCeilingMembershipScanCodeFixProvider), ct => ApplyAsync(context.Document, outer, inner, scan.Value, ct));
    }

    private static MembershipScan? Recognize(
        ForEachStatementSyntax inner,
        ForEachStatementSyntax outer,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (FindEqualityGuard(inner) is not { } found
            || SelectOuterSide(found.Comparison, outer) is not { } outerSide)
        {
            return null;
        }

        var comparison = found.Comparison;
        var innerSide = comparison.Left == outerSide ? comparison.Right : comparison.Left;

        if (FindStableCollection(inner, innerSide, outerSide, model, cancellationToken) is not { } collection
            || model.GetTypeInfo(innerSide, cancellationToken).Type is not { } elementType
            || !IsValueEquatable(elementType)
            || inner.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not { } method)
        {
            return null;
        }

        var collectionName = collection.Identifier.ValueText;

        return (found.Guard, outerSide, collectionName, UniqueName(collectionName + "Set", method));
    }

    private static (IfStatementSyntax Guard, BinaryExpressionSyntax Comparison)? FindEqualityGuard(
        ForEachStatementSyntax inner)
    {
        if (inner.Statement is not BlockSyntax body
            || body.Statements.Count is not 1
            || body.Statements[0] is not IfStatementSyntax guard
            || guard.Else is not null
            || !guard.Condition.IsKind(SyntaxKind.EqualsExpression)
            || !IsReturnGuard(guard, inner.Identifier.ValueText)
            || guard.Condition is not BinaryExpressionSyntax comparison)
        {
            return null;
        }

        return (guard, comparison);
    }

    private static bool IsReturnGuard(IfStatementSyntax guard, string innerName)
    {
        return ReturnsOnMatch(guard.Statement) && !ReturnsInnerVariable(guard.Statement, innerName);
    }

    private static ExpressionSyntax? SelectOuterSide(BinaryExpressionSyntax comparison, ForEachStatementSyntax outer)
    {
        var outerName = outer.Identifier.ValueText;
        if (RootsAt(comparison.Left, outerName))
        {
            return comparison.Left;
        }

        return RootsAt(comparison.Right, outerName) ? comparison.Right : null;
    }

    private static IdentifierNameSyntax? FindStableCollection(
        ForEachStatementSyntax inner,
        ExpressionSyntax innerSide,
        ExpressionSyntax outerSide,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (innerSide is not IdentifierNameSyntax innerIdentifier
            || !string.Equals(innerIdentifier.Identifier.ValueText, inner.Identifier.ValueText, StringComparison.Ordinal)
            || ContainsInvocation(outerSide)
            || inner.Expression is not IdentifierNameSyntax collection
            || !IsStableSource(collection, inner, model, cancellationToken))
        {
            return null;
        }

        return collection;
    }

    private static async Task<Document> ApplyAsync(
        Document document,
        ForEachStatementSyntax outer,
        ForEachStatementSyntax inner,
        MembershipScan scan,
        CancellationToken cancellationToken)
    {
        var (guard, outerSide, collectionName, setName) = scan;
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.InsertBefore(outer, SetDeclaration(collectionName, setName, outer));
        editor.ReplaceNode(inner, RewriteGuard(guard, ContainsCall(setName, outerSide), inner));

        var changed = editor.GetChangedDocument();
        var changedRoot = await changed.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        return changedRoot is null
            ? changed
            : changed.WithSyntaxRoot(UsingDirectives.AddIfMissing(changedRoot, "System.Linq"));
    }

    private static LocalDeclarationStatementSyntax SetDeclaration(string collectionName, string setName, SyntaxNode anchor)
    {
        var call = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(collectionName),
                SyntaxFactory.IdentifierName("ToHashSet")));

        var declarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(setName))
            .WithInitializer(SyntaxFactory.EqualsValueClause(call));

        return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(declarator)))
            .WithLeadingTrivia(IndentationTrivia(anchor))
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
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

    private static InvocationExpressionSyntax ContainsCall(string setName, ExpressionSyntax value)
    {
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(setName),
                SyntaxFactory.IdentifierName("Contains")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(value.WithLeadingTrivia().WithTrailingTrivia()))));
    }

    /// <summary>
    ///     Rebuilds the guard at the inner loop's indent. Replacing only the guard's leading
    ///     trivia re-indents the <c lang="csharp">if</c> keyword, and its block body keeps the
    ///     deeper indent of the loop the rewrite removes.
    /// </summary>
    private static StatementSyntax RewriteGuard(
        IfStatementSyntax guard,
        ExpressionSyntax condition,
        ForEachStatementSyntax inner)
    {
        var extra = FixSyntax.TrailingWhitespace(guard.GetLeadingTrivia()).ToString().Length
                    - FixSyntax.TrailingWhitespace(inner.GetLeadingTrivia()).ToString().Length;
        var lines = guard.WithCondition(condition).ToFullString().Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = Dedent(lines[index], extra);
        }

        return SyntaxFactory.ParseStatement(string.Join("\n", lines).TrimEnd())
            .WithTrailingTrivia(inner.GetTrailingTrivia());
    }

    private static string Dedent(string line, int count)
    {
        var removable = 0;

        while (removable < count && removable < line.Length && line[removable] == ' ')
        {
            removable++;
        }

        return line.Substring(removable);
    }

    /// <summary>
    ///     A match must leave the method. A <c lang="csharp">break</c> would exit the outer loop
    ///     once the inner loop is gone, and a <c lang="csharp">continue</c> would skip the
    ///     statements the rewrite removes.
    /// </summary>
    private static bool ReturnsOnMatch(StatementSyntax statement)
    {
        return statement is ReturnStatementSyntax
               || (statement is BlockSyntax { Statements.Count: 1 } block
                   && block.Statements[0] is ReturnStatementSyntax);
    }

    /// <summary>
    ///     A returned expression that reads the inner iteration variable has no meaning once that
    ///     loop is gone, so the fixed document would not compile.
    /// </summary>
    private static bool ReturnsInnerVariable(StatementSyntax statement, string name)
    {
        ReturnStatementSyntax? returned;
        if (statement is ReturnStatementSyntax single)
        {
            returned = single;
        }
        else if (statement is BlockSyntax { Statements.Count: 1 } block)
        {
            returned = block.Statements[0] as ReturnStatementSyntax;
        }
        else
        {
            returned = null;
        }

        return returned?.Expression is { } value
               && value.DescendantNodesAndSelf()
                   .OfType<IdentifierNameSyntax>()
                   .Any(identifier => string.Equals(identifier.Identifier.ValueText, name, StringComparison.Ordinal));
    }

    private static bool IsInsideLoop(ForEachStatementSyntax outer)
    {
        for (var current = outer.Parent; current is not null; current = current.Parent)
        {
            if (current is ForEachStatementSyntax or ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return true;
            }

            if (current is MethodDeclarationSyntax)
            {
                return false;
            }
        }

        return true;
    }

    private static bool RootsAt(ExpressionSyntax expression, string name)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => string.Equals(
                identifier.Identifier.ValueText,
                name,
                StringComparison.Ordinal),
            MemberAccessExpressionSyntax member => RootsAt(member.Expression, name),
            ElementAccessExpressionSyntax element => RootsAt(element.Expression, name),
            ParenthesizedExpressionSyntax parenthesized => RootsAt(parenthesized.Expression, name),
            _ => false
        };
    }

    private static bool ContainsInvocation(ExpressionSyntax expression)
    {
        return expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any();
    }

    /// <summary>
    ///     The set is built once, above the outer loop, so the source collection must be a local
    ///     or a parameter. Any other mention of the name in the method may read or write it, and a
    ///     write would leave the hoisted set stale, so the rewrite declines.
    /// </summary>
    private static bool IsStableSource(
        IdentifierNameSyntax collection,
        ForEachStatementSyntax inner,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(collection, cancellationToken).Symbol;
        if (symbol is not ILocalSymbol and not IParameterSymbol
            || inner.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not { } method)
        {
            return false;
        }

        foreach (var mention in method.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (mention.Span != collection.Span
                && string.Equals(mention.Identifier.ValueText, collection.Identifier.ValueText, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     <c lang="csharp">a == b</c> and the default set comparer agree only for a primitive,
    ///     a string, or an enumeration. Any other type may overload equality and change the
    ///     answer. A floating point element diverges even without an overload, because the set
    ///     comparer reports two NaN values as equal and <c lang="csharp">==</c> never does.
    /// </summary>
    private static bool IsValueEquatable(ITypeSymbol type)
    {
        return type.TypeKind is TypeKind.Enum
               || type.SpecialType is not (SpecialType.None
                   or SpecialType.System_Object
                   or SpecialType.System_Single
                   or SpecialType.System_Double);
    }

    private static string UniqueName(string candidate, SyntaxNode scope)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in scope.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                taken.Add(token.ValueText);
            }
        }

        var name = candidate;
        var suffix = 2;
        while (true)
        {
            if (!taken.Contains(name))
            {
                return name;
            }

            name = candidate + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }
    }
}
