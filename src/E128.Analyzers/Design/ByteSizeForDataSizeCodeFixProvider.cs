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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ByteSizeForDataSizeCodeFixProvider))]
[Shared]
public sealed class ByteSizeForDataSizeCodeFixProvider : CodeFixProvider
{
    private const string PugClassesNamespace = "Pug.Core.Classes";
    private const string ByteSizeTypeName = "ByteSize";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ByteSizeForDataSizeAnalyzer.DiagnosticId];

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
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);

        context.RegisterCodeFix(
            CodeAction.Create(
                "Change type to ByteSize",
                ct => ApplyFixAsync(context.Document, root, token, ct),
                nameof(ByteSizeForDataSizeCodeFixProvider)),
            diagnostic);
    }

    private static Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode root,
        SyntaxToken token,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var newRoot = token.Parent switch
        {
            _ when FindAncestor<LocalDeclarationStatementSyntax>(token) is { } local =>
                FixLocalDeclaration(root, local),

            _ when FindAncestor<PropertyDeclarationSyntax>(token) is { } property =>
                FixPropertyDeclaration(root, property),

            _ when FindAncestor<ParameterSyntax>(token) is { } parameter =>
                FixParameterDeclaration(root, parameter),

            _ when FindAncestor<FieldDeclarationSyntax>(token) is { } field =>
                FixFieldDeclaration(root, field),

            _ => root
        };

        newRoot = EnsureUsingDirective(newRoot);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static SyntaxNode FixLocalDeclaration(SyntaxNode root, LocalDeclarationStatementSyntax local)
    {
        var declaration = local.Declaration;
        var byteSizeType = CreateByteSizeTypeSyntax();

        var newDeclaration = declaration.WithType(byteSizeType.WithTrailingTrivia(SyntaxFactory.Space));

        if (declaration.Variables.Count == 1 && declaration.Variables[0].Initializer is { } initializer)
        {
            var wrapped = WrapInFromBytes(initializer.Value);
            var newVariable = declaration.Variables[0].WithInitializer(initializer.WithValue(wrapped));
            newDeclaration = newDeclaration.WithVariables(SyntaxFactory.SingletonSeparatedList(newVariable));
        }

        var newLocal = local
            .WithModifiers(RemoveConstModifier(local.Modifiers))
            .WithDeclaration(newDeclaration);

        return root.ReplaceNode(local, newLocal);
    }

    private static SyntaxNode FixPropertyDeclaration(SyntaxNode root, PropertyDeclarationSyntax property)
    {
        var byteSizeType = CreateByteSizeTypeSyntax();
        var newProperty = property.WithType(byteSizeType.WithTrailingTrivia(SyntaxFactory.Space));

        if (property.Initializer is { } initializer)
        {
            var wrapped = WrapInFromBytes(initializer.Value);
            newProperty = newProperty.WithInitializer(initializer.WithValue(wrapped));
        }

        return root.ReplaceNode(property, newProperty);
    }

    private static SyntaxNode FixParameterDeclaration(SyntaxNode root, ParameterSyntax parameter)
    {
        if (parameter.Type is null)
        {
            return root;
        }

        var byteSizeType = CreateByteSizeTypeSyntax();
        var newParameter = parameter.WithType(byteSizeType.WithTrailingTrivia(SyntaxFactory.Space));

        if (parameter.Default is { } defaultValue)
        {
            var wrapped = WrapInFromBytes(defaultValue.Value);
            newParameter = newParameter.WithDefault(defaultValue.WithValue(wrapped));
        }

        return root.ReplaceNode(parameter, newParameter);
    }

    private static SyntaxNode FixFieldDeclaration(SyntaxNode root, FieldDeclarationSyntax field)
    {
        var declaration = field.Declaration;
        var byteSizeType = CreateByteSizeTypeSyntax();
        var newDeclaration = declaration.WithType(byteSizeType.WithTrailingTrivia(SyntaxFactory.Space));

        if (declaration.Variables.Count == 1 && declaration.Variables[0].Initializer is { } initializer)
        {
            var wrapped = WrapInFromBytes(initializer.Value);
            var newVariable = declaration.Variables[0].WithInitializer(initializer.WithValue(wrapped));
            newDeclaration = newDeclaration.WithVariables(SyntaxFactory.SingletonSeparatedList(newVariable));
        }

        var newModifiers = field.Modifiers;
        if (newModifiers.Any(SyntaxKind.ConstKeyword))
        {
            newModifiers = RemoveConstModifier(newModifiers);
            if (!newModifiers.Any(SyntaxKind.StaticKeyword))
            {
                newModifiers = newModifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space));
            }

            newModifiers = newModifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        }

        var newField = field
            .WithModifiers(newModifiers)
            .WithDeclaration(newDeclaration);

        return root.ReplaceNode(field, newField);
    }

    private static InvocationExpressionSyntax WrapInFromBytes(ExpressionSyntax expression)
    {
        var qualifiedName = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("Pug"),
                        SyntaxFactory.IdentifierName("Core")),
                    SyntaxFactory.IdentifierName("Classes")),
                SyntaxFactory.IdentifierName(ByteSizeTypeName)),
            SyntaxFactory.IdentifierName("FromBytes"));

        return SyntaxFactory.InvocationExpression(
                qualifiedName,
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(expression.WithoutTrivia()))))
            .WithTriviaFrom(expression);
    }

    private static IdentifierNameSyntax CreateByteSizeTypeSyntax()
    {
        return SyntaxFactory.IdentifierName(ByteSizeTypeName);
    }

    private static SyntaxTokenList RemoveConstModifier(SyntaxTokenList modifiers)
    {
        return SyntaxFactory.TokenList(modifiers.Where(m => !m.IsKind(SyntaxKind.ConstKeyword)));
    }

    private static SyntaxNode EnsureUsingDirective(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        var hasUsing = compilationUnit.Usings.Any(u =>
            string.Equals(u.Name?.ToString(), PugClassesNamespace, StringComparison.Ordinal));

        if (hasUsing)
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName(PugClassesNamespace))
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return compilationUnit.WithUsings(compilationUnit.Usings.Insert(0, usingDirective));
    }

    private static T? FindAncestor<T>(SyntaxToken token) where T : SyntaxNode
    {
        return token.Parent?.AncestorsAndSelf().OfType<T>().FirstOrDefault();
    }
}
