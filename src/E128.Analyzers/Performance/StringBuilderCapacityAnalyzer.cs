using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StringBuilderCapacityAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128215";

    private static readonly ImmutableArray<string> AppendMethods = ["Append", "AppendLine"];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "StringBuilder created without capacity for a known append count",
        "'{0}' is appended in a loop with a known count — pass an initial capacity",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var stringBuilder = startContext.Compilation.GetTypeByMetadataName("System.Text.StringBuilder");
            if (stringBuilder is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeDeclaration(nodeContext, stringBuilder),
                SyntaxKind.LocalDeclarationStatement);
        });
    }

    private static void AnalyzeDeclaration(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol stringBuilder)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (declaration.Declaration.Variables.Count != 1)
        {
            return;
        }

        var variable = declaration.Declaration.Variables[0];
        if (variable.Initializer?.Value is not BaseObjectCreationExpressionSyntax creation
            || creation.ArgumentList is { Arguments.Count: > 0 }
            || SetsCapacityInInitializer(creation))
        {
            return;
        }

        var createdType = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;
        if (!SymbolEqualityComparer.Default.Equals(createdType, stringBuilder))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol local)
        {
            return;
        }

        var loop = LoopCount.TryGetNextFillLoop(
            declaration,
            local,
            AppendMethods,
            context.SemanticModel,
            context.CancellationToken);
        if (loop is null)
        {
            return;
        }

        if (LoopCount.TryGetCount(loop, context.SemanticModel, context.CancellationToken) is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            creation.GetLocation(),
            variable.Identifier.ValueText));
    }

    private static bool SetsCapacityInInitializer(BaseObjectCreationExpressionSyntax creation)
    {
        if (creation.Initializer is null)
        {
            return false;
        }

        foreach (var expression in creation.Initializer.Expressions)
        {
            if (expression is AssignmentExpressionSyntax assignment
                && string.Equals(GetAssignedName(assignment.Left), "Capacity", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetAssignedName(ExpressionSyntax left)
    {
        return left switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null
        };
    }
}
