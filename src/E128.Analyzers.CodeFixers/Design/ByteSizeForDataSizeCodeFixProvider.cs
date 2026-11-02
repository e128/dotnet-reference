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
public sealed class ByteSizeForDataSizeCodeFixProvider : E128CodeFixProviderBase
{
    private const string PugClassesNamespace = "Pug.Core.Classes";
    private const string ByteSizeTypeName = "ByteSize";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ByteSizeForDataSizeAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);

        // A parameter default is a compile-time constant, and ByteSize.FromBytes is a method call, so
        // the rewritten parameter gives CS1736. The developer has to pick the unit, so no fix is offered.
        if (FindAncestor<ParameterSyntax>(token) is { Default: not null })
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Change type to ByteSize",
                ct => ApplyFixAsync(context.Document, root, token, ct),
                nameof(ByteSizeForDataSizeCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
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

        newRoot = UsingDirectives.AddIfMissing(newRoot, PugClassesNamespace);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static SyntaxNode FixLocalDeclaration(SyntaxNode root, LocalDeclarationStatementSyntax local)
    {
        var declaration = local.Declaration;
        var byteSizeType = CreateByteSizeTypeSyntax();

        var newDeclaration = WrapEveryInitializer(declaration)
            .WithType(byteSizeType.WithTrailingTrivia(SyntaxFactory.Space));

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
        var newDeclaration = WrapEveryInitializer(declaration)
            .WithType(byteSizeType.WithTrailingTrivia(SyntaxFactory.Space));

        var newModifiers = field.Modifiers;
        if (newModifiers.Any(SyntaxKind.ConstKeyword))
        {
            newModifiers = RemoveConstModifier(newModifiers);
            newModifiers = InModifierHelper.AddModifier(newModifiers, SyntaxKind.StaticKeyword);
            newModifiers = InModifierHelper.AddModifier(newModifiers, SyntaxKind.ReadOnlyKeyword);
        }

        var newField = field
            .WithModifiers(newModifiers)
            .WithDeclaration(newDeclaration);

        return root.ReplaceNode(field, newField);
    }

    // Every declarator shares the new type, so every initializer must carry the conversion. A field
    // or local with more than one declarator would otherwise assign an int to ByteSize.
    private static VariableDeclarationSyntax WrapEveryInitializer(VariableDeclarationSyntax declaration)
    {
        var variables = declaration.Variables.Select(variable =>
            variable.Initializer is { } initializer
                ? variable.WithInitializer(initializer.WithValue(WrapInFromBytes(initializer.Value)))
                : variable);

        return declaration.WithVariables(SyntaxFactory.SeparatedList(variables));
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

    private static T? FindAncestor<T>(SyntaxToken token) where T : SyntaxNode
    {
        return token.Parent?.AncestorsAndSelf().OfType<T>().FirstOrDefault();
    }
}
