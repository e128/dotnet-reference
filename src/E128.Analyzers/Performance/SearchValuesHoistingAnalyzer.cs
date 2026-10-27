using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SearchValuesHoistingAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128124";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Cache SearchValues in a static readonly field",
        "SearchValues.Create builds an immutable table on each call. Move it to a static readonly field.",
        "Performance",
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
        context.RegisterCompilationStartAction(start =>
        {
            var searchValues = MetadataTypes.Find(start.Compilation, "System.Buffers.SearchValues");
            if (searchValues.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, searchValues),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> searchValues)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(memberAccess.Name.Identifier.ValueText, "Create", StringComparison.Ordinal))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !MetadataTypes.Contains(searchValues, method.ContainingType))
        {
            return;
        }

        if (IsInsideCachedField(invocation) || !IsHoistable(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    // The fix moves the call into a static initializer, so every input must be
    // a constant or static readonly value. A local, parameter or instance member would not compile.
    internal static bool IsHoistable(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (semanticModel.GetConstantValue(argument.Expression, cancellationToken).HasValue)
            {
                continue;
            }

            foreach (var name in argument.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(name, cancellationToken).Symbol;
                var isStaticInput = symbol is INamespaceOrTypeSymbol or IMethodSymbol
                    || symbol is IFieldSymbol { IsStatic: true } field && (field.IsConst || field.IsReadOnly);

                if (!isStaticInput)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsInsideCachedField(InvocationExpressionSyntax invocation)
    {
        foreach (var ancestor in invocation.Ancestors())
        {
            if (ancestor is PropertyDeclarationSyntax property)
            {
                // A static property initializer runs once, so it caches like a static readonly field.
                return property.Modifiers.Any(SyntaxKind.StaticKeyword)
                    && property.Initializer is not null
                    && property.Initializer.Span.Contains(invocation.Span);
            }

            if (ancestor is FieldDeclarationSyntax field)
            {
                var isStatic = false;
                var isReadOnly = false;

                foreach (var modifier in field.Modifiers)
                {
                    if (modifier.IsKind(SyntaxKind.StaticKeyword))
                    {
                        isStatic = true;
                    }

                    if (modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
                    {
                        isReadOnly = true;
                    }
                }

                return isStatic && isReadOnly;
            }

            if (ancestor is StatementSyntax or MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }
}
