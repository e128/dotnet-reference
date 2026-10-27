using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128109: stackalloc must sit outside loops and have a bounded length of at most 1024 bytes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StackAllocSafetyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128109";

    private const int MaxStackBytes = 1024;

    private static readonly Dictionary<SpecialType, int> ElementSizes = new()
    {
        [SpecialType.System_Int16] = 2,
        [SpecialType.System_UInt16] = 2,
        [SpecialType.System_Char] = 2,
        [SpecialType.System_Int32] = 4,
        [SpecialType.System_UInt32] = 4,
        [SpecialType.System_Single] = 4,
        [SpecialType.System_Int64] = 8,
        [SpecialType.System_UInt64] = 8,
        [SpecialType.System_Double] = 8,
        [SpecialType.System_Decimal] = 16
    };

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "stackalloc must be bounded and outside loops",
        "stackalloc is unsafe here: {0}",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Stack space is not reclaimed until the method returns. Keep stackalloc out of loops, cap it at " +
        "1024 bytes, and fall back to ArrayPool or a heap array above the cap.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.StackAllocArrayCreationExpression,
            SyntaxKind.ImplicitStackAllocArrayCreationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var reason = Classify(context.Node, context.SemanticModel, context.CancellationToken);
        if (reason is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), reason));
        }
    }

    private static string? Classify(SyntaxNode node, SemanticModel model, System.Threading.CancellationToken cancellationToken)
    {
        if (IsInsideLoop(node))
        {
            return "it is inside a loop, and stack space is not reclaimed until the method returns";
        }

        if (model.GetTypeInfo(node, cancellationToken).ConvertedType is IPointerTypeSymbol)
        {
            return "it is assigned to a pointer, so assign it to Span<T> or ReadOnlySpan<T> for bounds checks";
        }

        if (node is not StackAllocArrayCreationExpressionSyntax { Type: ArrayTypeSyntax array }
            || array.RankSpecifiers.Count == 0
            || array.RankSpecifiers[0].Sizes.Count == 0
            || array.RankSpecifiers[0].Sizes[0] is OmittedArraySizeExpressionSyntax)
        {
            return null;
        }

        var size = array.RankSpecifiers[0].Sizes[0];
        var constant = model.GetConstantValue(size, cancellationToken);

        if (constant.HasValue)
        {
            if (constant.Value is int count)
            {
                var bytes = (long)count * ElementSize(model.GetTypeInfo(array.ElementType, cancellationToken).Type);
                if (bytes > MaxStackBytes)
                {
                    return "its size is " + bytes.ToString(CultureInfo.InvariantCulture) + " bytes, above the 1024 byte limit";
                }
            }

            return null;
        }

        return IsBounded(node, size, model, cancellationToken)
            ? null
            : "its length is not bounded, so guard it and fall back to the heap above 1024 bytes";
    }

    private static bool IsInsideLoop(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return true;
            }

            if (ancestor is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsBounded(
        SyntaxNode node,
        ExpressionSyntax size,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        if (size is InvocationExpressionSyntax invocation
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol
            {
                Name: "Min",
                ContainingType.Name: "Math"
            })
        {
            return true;
        }

        var lengthNames = new HashSet<string>(
            size.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Select(n => n.Identifier.ValueText),
            StringComparer.Ordinal);

        var child = node;
        foreach (var ancestor in node.Ancestors())
        {
            // A guard counts only when its condition tests a name used in the length.
            if ((ancestor is ConditionalExpressionSyntax conditional && conditional.Condition != child
                 && MentionsAny(conditional.Condition, lengthNames))
                || (ancestor is IfStatementSyntax ifStatement && ifStatement.Condition != child
                    && MentionsAny(ifStatement.Condition, lengthNames)))
            {
                return true;
            }

            if (ancestor is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }

            child = ancestor;
        }

        return false;
    }

    private static bool MentionsAny(ExpressionSyntax condition, HashSet<string> names)
    {
        return condition.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(n => names.Contains(n.Identifier.ValueText));
    }

    private static int ElementSize(ITypeSymbol? type)
    {
        return type is not null && ElementSizes.TryGetValue(type.SpecialType, out var size) ? size : 1;
    }
}
