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

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StaticNumericIncrementCodeFixProvider))]
[Shared]
public sealed class StaticNumericIncrementCodeFixProvider : CodeFixProvider
{
    private const string ThreadingNamespace = "System.Threading";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [StaticNumericIncrementAnalyzer.DiagnosticId];

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
        var tokenNode = root.FindNode(diagnostic.Location.SourceSpan);
        var expression = ResolveExpression(tokenNode);
        if (expression is null)
        {
            return;
        }

        var fieldOperand = ResolveFieldOperand(expression);
        if (fieldOperand is null)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(fieldOperand, context.CancellationToken);
        if (symbolInfo.Symbol is not IFieldSymbol fieldSymbol)
        {
            return;
        }

        var isIntOrLong = fieldSymbol.Type.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64;

        RegisterInterlockedFix(context, diagnostic, expression, fieldOperand, isIntOrLong);
        RegisterRemoveStaticFix(context, diagnostic, fieldSymbol);
    }

    private static ExpressionSyntax? ResolveExpression(SyntaxNode tokenNode)
    {
        return tokenNode switch
        {
            PrefixUnaryExpressionSyntax p => p,
            PostfixUnaryExpressionSyntax p => p,
            AssignmentExpressionSyntax a when a.IsKind(SyntaxKind.AddAssignmentExpression)
                || a.IsKind(SyntaxKind.SubtractAssignmentExpression) => a,
            _ => tokenNode.Parent switch
            {
                PrefixUnaryExpressionSyntax p => p,
                PostfixUnaryExpressionSyntax p => p,
                AssignmentExpressionSyntax a when a.IsKind(SyntaxKind.AddAssignmentExpression)
                    || a.IsKind(SyntaxKind.SubtractAssignmentExpression) => a,
                _ => null
            }
        };
    }

    private static ExpressionSyntax? ResolveFieldOperand(ExpressionSyntax expression)
    {
        return expression switch
        {
            PrefixUnaryExpressionSyntax p => p.Operand,
            PostfixUnaryExpressionSyntax p => p.Operand,
            AssignmentExpressionSyntax a => a.Left,
            _ => null
        };
    }

    private static void RegisterInterlockedFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        ExpressionSyntax expression,
        ExpressionSyntax fieldOperand,
        bool isIntOrLong)
    {
        if (!isIntOrLong)
        {
            return;
        }

        var interlockedTitle = expression switch
        {
            PrefixUnaryExpressionSyntax => "Use Interlocked.Decrement/Increment",
            PostfixUnaryExpressionSyntax => "Use Interlocked.Decrement/Increment",
            AssignmentExpressionSyntax a when a.IsKind(SyntaxKind.AddAssignmentExpression) =>
                "Use Interlocked.Add",
            AssignmentExpressionSyntax a when a.IsKind(SyntaxKind.SubtractAssignmentExpression) =>
                "Use Interlocked.Add",
            _ => "Use Interlocked methods"
        };

        context.RegisterCodeFix(
            CodeAction.Create(
                interlockedTitle,
                ct => ApplyInterlockedFixAsync(context.Document, expression, fieldOperand, ct),
                $"{StaticNumericIncrementAnalyzer.DiagnosticId}Interlocked"),
            diagnostic);
    }

    private static void RegisterRemoveStaticFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        IFieldSymbol fieldSymbol)
    {
        if (IsContainingTypeStaticClass(fieldSymbol))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove 'static' keyword",
                ct => ApplyRemoveStaticFixAsync(context.Document, fieldSymbol, ct),
                $"{StaticNumericIncrementAnalyzer.DiagnosticId}RemoveStatic"),
            diagnostic);
    }

    private static bool IsContainingTypeStaticClass(IFieldSymbol fieldSymbol)
    {
        var containingType = fieldSymbol.ContainingType;
        while (containingType is not null)
        {
            if (containingType.IsStatic)
            {
                return true;
            }
            containingType = containingType.ContainingType;
        }

        return false;
    }

    private static async Task<Document> ApplyInterlockedFixAsync(
        Document document,
        ExpressionSyntax expression,
        ExpressionSyntax fieldOperand,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var interlockedInvocation = BuildInterlockedInvocation(expression, fieldOperand);
        var newRoot = AddUsingIfMissing(root.ReplaceNode(expression, interlockedInvocation));
        return document.WithSyntaxRoot(newRoot);
    }

    private static AssignmentExpressionSyntax BuildInterlockedInvocation(
        ExpressionSyntax expression,
        ExpressionSyntax fieldOperand)
    {
        var refArgument = SyntaxFactory.Argument(
            null, SyntaxFactory.Token(SyntaxKind.RefKeyword),
            fieldOperand.WithoutTrivia());

        (var methodName, var extraArg) = expression switch
        {
            PrefixUnaryExpressionSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) =>
                ("Increment", null),
            PrefixUnaryExpressionSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.MinusMinusToken) =>
                ("Decrement", null),
            PostfixUnaryExpressionSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) =>
                ("Increment", null),
            PostfixUnaryExpressionSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.MinusMinusToken) =>
                ("Decrement", null),
            AssignmentExpressionSyntax compound when compound.IsKind(SyntaxKind.AddAssignmentExpression) =>
                ("Add", compound.Right.WithoutTrivia()),
            AssignmentExpressionSyntax compound when compound.IsKind(SyntaxKind.SubtractAssignmentExpression) =>
                ("Add", SyntaxFactory.PrefixUnaryExpression(
                    SyntaxKind.UnaryMinusExpression, compound.Right.WithoutTrivia())),
            _ => ("Increment", null)
        };

        var arguments = extraArg is not null
            ? SyntaxFactory.SeparatedList(new[] { refArgument, SyntaxFactory.Argument(extraArg) })
            : SyntaxFactory.SeparatedList(new[] { refArgument });

        var interlockedInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("Interlocked"),
                SyntaxFactory.IdentifierName(methodName)))
            .WithArgumentList(SyntaxFactory.ArgumentList(arguments));

        return SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName("_"),
            interlockedInvocation);
    }

    private static async Task<Document> ApplyRemoveStaticFixAsync(
        Document document,
        IFieldSymbol fieldSymbol,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var declaringRef = fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (declaringRef is null)
        {
            return document;
        }

        var declaringNode = await declaringRef.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
        if (declaringNode is null)
        {
            return document;
        }

        var fieldDeclaration = declaringNode.AncestorsAndSelf()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault();

        if (fieldDeclaration is null)
        {
            return document;
        }

        var newModifiers = fieldDeclaration.Modifiers
            .Where(m => !m.IsKind(SyntaxKind.StaticKeyword))
            .ToArray();

        var newFieldDeclaration = newModifiers.Length > 0
            ? fieldDeclaration.WithModifiers(SyntaxFactory.TokenList(newModifiers))
            : fieldDeclaration.WithModifiers(default);

        var newRoot = root.ReplaceNode(fieldDeclaration, newFieldDeclaration);
        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode AddUsingIfMissing(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        if (compilationUnit.Usings.Any(u =>
                string.Equals(u.Name?.ToString(), ThreadingNamespace, StringComparison.Ordinal)))
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ThreadingNamespace))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        var insertIndex = 0;
        for (var i = 0; i < compilationUnit.Usings.Count; i++)
        {
            if (StringComparer.Ordinal.Compare(
                    compilationUnit.Usings[i].Name?.ToString(), ThreadingNamespace) < 0)
            {
                insertIndex = i + 1;
            }
        }

        return compilationUnit.WithUsings(compilationUnit.Usings.Insert(insertIndex, usingDirective));
    }
}
