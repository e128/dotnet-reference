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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DirectHttpClientInstantiationCodeFixProvider))]
[Shared]
public sealed class DirectHttpClientInstantiationCodeFixProvider : CodeFixProvider
{
    private const string FactoryInterfaceName = "IHttpClientFactory";
    private const string DefaultFieldName = "_httpClientFactory";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DirectHttpClientInstantiationAnalyzer.DiagnosticId];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not ObjectCreationExpressionSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace with IHttpClientFactory.CreateClient()",
                ct => ApplyFixAsync(context.Document, node, ct),
                nameof(DirectHttpClientInstantiationCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode creationNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var creation = (ObjectCreationExpressionSyntax)creationNode;
        var classDecl = creation.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl is null)
        {
            return document;
        }

        var existingFieldName = FindExistingFactoryField(classDecl);

        var fieldName = existingFieldName ?? DefaultFieldName;

        var replacement = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(fieldName),
                SyntaxFactory.IdentifierName("CreateClient")));

        var annotation = new SyntaxAnnotation("E128004Fix");
        replacement = replacement.WithAdditionalAnnotations(annotation)
            .WithTriviaFrom(creation);

        var newRoot = root.ReplaceNode(creation, replacement);

        if (existingFieldName is not null)
        {
            return document.WithSyntaxRoot(newRoot);
        }

        var newClassDecl = newRoot.GetAnnotatedNodes(annotation)
            .FirstOrDefault()
            ?.FirstAncestorOrSelf<ClassDeclarationSyntax>();

        if (newClassDecl is null)
        {
            return document.WithSyntaxRoot(newRoot);
        }

        var updatedClass = AddFieldAndConstructorParameter(newClassDecl, fieldName);
        newRoot = newRoot.ReplaceNode(newClassDecl, updatedClass);

        return document.WithSyntaxRoot(newRoot);
    }

    private static string? FindExistingFactoryField(ClassDeclarationSyntax classDecl)
    {
        foreach (var member in classDecl.Members)
        {
            if (member is not FieldDeclarationSyntax field)
            {
                continue;
            }

            var typeName = field.Declaration.Type.ToString();
            if (!string.Equals(typeName, FactoryInterfaceName, StringComparison.Ordinal))
            {
                continue;
            }

            var variable = field.Declaration.Variables.FirstOrDefault();
            if (variable is not null)
            {
                return variable.Identifier.ValueText;
            }
        }

        return null;
    }

    private static ClassDeclarationSyntax AddFieldAndConstructorParameter(
        ClassDeclarationSyntax classDecl,
        string fieldName)
    {
        var parameterName = TrimLeadingUnderscore(fieldName);

        var fieldDecl = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.IdentifierName(FactoryInterfaceName))
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(fieldName))))
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        var existingCtor = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(c => c.Modifiers.Any(SyntaxKind.PublicKeyword));

        return existingCtor is not null
            ? AddToExistingConstructor(classDecl, existingCtor, fieldDecl, fieldName, parameterName)
            : AddNewConstructor(classDecl, fieldDecl, fieldName, parameterName);
    }

    private static ClassDeclarationSyntax AddToExistingConstructor(
        ClassDeclarationSyntax classDecl,
        ConstructorDeclarationSyntax existingCtor,
        FieldDeclarationSyntax fieldDecl,
        string fieldName,
        string parameterName)
    {
        var newParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.IdentifierName(FactoryInterfaceName))
            .WithLeadingTrivia(SyntaxFactory.Space);

        var newParamList = existingCtor.ParameterList.AddParameters(newParam);

        var assignment = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(fieldName),
                SyntaxFactory.IdentifierName(parameterName)));

        var newBody = existingCtor.Body is not null
            ? existingCtor.Body.AddStatements(assignment)
            : SyntaxFactory.Block(assignment);

        var newCtor = existingCtor
            .WithParameterList(newParamList)
            .WithBody(newBody);

        var classWithField = InsertFieldBeforeConstructor(classDecl, fieldDecl, existingCtor);
        return classWithField.ReplaceNode(
            classWithField.Members.OfType<ConstructorDeclarationSyntax>()
                .First(c => c.Modifiers.Any(SyntaxKind.PublicKeyword)),
            newCtor);
    }

    private static ClassDeclarationSyntax AddNewConstructor(
        ClassDeclarationSyntax classDecl,
        FieldDeclarationSyntax fieldDecl,
        string fieldName,
        string parameterName)
    {
        var className = classDecl.Identifier.ValueText;

        var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.IdentifierName(FactoryInterfaceName));

        var assignment = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(fieldName),
                SyntaxFactory.IdentifierName(parameterName)));

        var ctor = SyntaxFactory.ConstructorDeclaration(className)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(SyntaxFactory.ParameterList(
                SyntaxFactory.SingletonSeparatedList(param)))
            .WithBody(SyntaxFactory.Block(assignment))
            .WithLeadingTrivia(SyntaxFactory.LineFeed)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        var firstMethodIndex = -1;
        for (var i = 0; i < classDecl.Members.Count; i++)
        {
            if (classDecl.Members[i] is MethodDeclarationSyntax)
            {
                firstMethodIndex = i;
                break;
            }
        }

        if (firstMethodIndex >= 0)
        {
            var members = classDecl.Members
                .Insert(firstMethodIndex, ctor)
                .Insert(firstMethodIndex, fieldDecl);
            return classDecl.WithMembers(members);
        }

        return classDecl.AddMembers(fieldDecl, ctor);
    }

    private static ClassDeclarationSyntax InsertFieldBeforeConstructor(
        ClassDeclarationSyntax classDecl,
        FieldDeclarationSyntax fieldDecl,
        ConstructorDeclarationSyntax existingCtor)
    {
        var ctorIndex = classDecl.Members.IndexOf(existingCtor);
        if (ctorIndex < 0)
        {
            return classDecl.AddMembers(fieldDecl);
        }

        var members = classDecl.Members.Insert(ctorIndex, fieldDecl);
        return classDecl.WithMembers(members);
    }

    private static string TrimLeadingUnderscore(string name)
    {
        return name.Length > 1 && name[0] == '_'
            ? name.Substring(1)
            : name;
    }
}
