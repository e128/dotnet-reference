using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StringBuilderPoolAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128081";

    private const string StringBuilderMetadataName = "System.Text.StringBuilder";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use StringBuilderPool instead of new StringBuilder()",
        "Use StringBuilderPool.Shared.Rent() instead of new StringBuilder() to reduce allocation pressure",
        "Performance",
        DiagnosticSeverity.Info,
        true,
        "Direct StringBuilder allocations can be replaced with StringBuilderPool.Shared.Rent() " +
        "from Pug.Text to reuse pooled instances and reduce GC pressure.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var stringBuilderType = compilationContext.Compilation.GetTypeByMetadataName(StringBuilderMetadataName);
            if (stringBuilderType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeObjectCreation(nodeContext, stringBuilderType),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    private static void AnalyzeObjectCreation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol stringBuilderType)
    {
        if (IsInsidePoolImplementation(context.Node))
        {
            return;
        }

        var typeInfo = GetConstructedType(context);
        if (typeInfo is null)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(typeInfo, stringBuilderType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation()));
    }

    private static ITypeSymbol? GetConstructedType(SyntaxNodeAnalysisContext context)
    {
        return context.Node switch
        {
            ObjectCreationExpressionSyntax creation =>
                context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type,
            ImplicitObjectCreationExpressionSyntax implicitCreation =>
                context.SemanticModel.GetTypeInfo(implicitCreation, context.CancellationToken).ConvertedType,
            _ => null
        };
    }

    private static bool IsInsidePoolImplementation(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current is ClassDeclarationSyntax classDecl &&
                string.Equals(classDecl.Identifier.Text, "DefaultStringBuilderPool", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }
}
