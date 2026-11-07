using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerNoDecompressionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128214";

    private const string PropertyName = "AutomaticDecompression";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "HTTP handler does not enable automatic decompression",
        "'{0}' does not set 'AutomaticDecompression' — compressed responses stay compressed",
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
            var handlers = new[]
            {
                startContext.Compilation.GetTypeByMetadataName("System.Net.Http.HttpClientHandler"),
                startContext.Compilation.GetTypeByMetadataName("System.Net.Http.SocketsHttpHandler")
            };

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeCreation(nodeContext, handlers),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol?[] handlers)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        var createdType = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;
        if (createdType is null || !IsTargetHandler(createdType, handlers))
        {
            return;
        }

        if (InitializerAssignments.Assigns(creation, PropertyName))
        {
            return;
        }

        if (SetsPropertyAfterCreation(creation, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            creation.GetLocation(),
            createdType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool IsTargetHandler(ITypeSymbol type, INamedTypeSymbol?[] handlers)
    {
        foreach (var handler in handlers)
        {
            if (handler is not null && SymbolEqualityComparer.Default.Equals(type, handler))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SetsPropertyAfterCreation(
        BaseObjectCreationExpressionSyntax creation,
        SyntaxNodeAnalysisContext context)
    {
        if (!TryGetTarget(creation, context, out var target))
        {
            return false;
        }

        // A field or property initializer has no block to scan. Treat a non-local target
        // as configured so the rule never reports a shared handler that a constructor sets.
        if (target is not ILocalSymbol)
        {
            return true;
        }

        if (creation.Ancestors().OfType<BlockSyntax>().FirstOrDefault() is not { } block)
        {
            return false;
        }

        var statement = creation.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        var start = statement is null ? 0 : block.Statements.IndexOf(statement) + 1;

        for (var i = start; i < block.Statements.Count; i++)
        {
            foreach (var assignment in block.Statements[i].DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.Left is not MemberAccessExpressionSyntax access
                    || !string.Equals(access.Name.Identifier.ValueText, PropertyName, StringComparison.Ordinal))
                {
                    continue;
                }

                var receiver = context.SemanticModel
                    .GetSymbolInfo(access.Expression, context.CancellationToken)
                    .Symbol;
                if (SymbolEqualityComparer.Default.Equals(receiver, target))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetTarget(
        BaseObjectCreationExpressionSyntax creation,
        SyntaxNodeAnalysisContext context,
        out ISymbol? target)
    {
        target = null;

        if (creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            target = context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken);
            return target is not null;
        }

        if (creation.Parent is AssignmentExpressionSyntax assignment
            && ReferenceEquals(assignment.Right, creation))
        {
            target = context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol;
            return target is not null;
        }

        return false;
    }
}
