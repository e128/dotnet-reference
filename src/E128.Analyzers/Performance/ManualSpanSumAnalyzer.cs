using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128135: A loop adds the elements of a float or double sequence one at a time.
///     <c lang="csharp">TensorPrimitives.Sum</c> uses the vector path.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManualSpanSumAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128135";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use TensorPrimitives.Sum instead of a sum loop",
        "The loop adds the elements one at a time. Use TensorPrimitives.Sum.",
        "Performance",
        DiagnosticSeverity.Warning,
        true,
        "TensorPrimitives.Sum reads the span in vector-width blocks. The loop adds one element " +
        "per iteration and carries a loop overhead on every step.");

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
            var tensors = start.Compilation.GetTypeByMetadataName("System.Numerics.Tensors.TensorPrimitives");
            var readOnlySpan = start.Compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
            var span = start.Compilation.GetTypeByMetadataName("System.Span`1");

            if (tensors is null || readOnlySpan is null || span is null)
            {
                return;
            }

            var floatType = start.Compilation.GetSpecialType(SpecialType.System_Single);
            var doubleType = start.Compilation.GetSpecialType(SpecialType.System_Double);

            if (floatType.TypeKind == TypeKind.Error || doubleType.TypeKind == TypeKind.Error)
            {
                return;
            }

            var supported = new ITypeSymbol[] { floatType, doubleType }
                .Where(type => HasSum(tensors, readOnlySpan.Construct(type)))
                .ToImmutableArray();

            if (supported.IsEmpty)
            {
                return;
            }

            var context2 = new SumContext(supported, readOnlySpan, span);

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, context2),
                SyntaxKind.ForEachStatement);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, SumContext sumContext)
    {
        if (context.Node is not ForEachStatementSyntax loop
            || !TryMatchLoop(loop, out var sumName, out _))
        {
            return;
        }

        var model = context.SemanticModel;

        if (FindType(model, loop, sumName, context.CancellationToken) is not { } sumType
            || sumType.SpecialType is not (SpecialType.System_Single or SpecialType.System_Double)
            || !sumContext.SupportedTypes.Contains(sumType, SymbolEqualityComparer.Default))
        {
            return;
        }

        if (GetElementType(model, loop.Expression, sumContext, context.CancellationToken) is not { } elementType
            || !SymbolEqualityComparer.Default.Equals(elementType, sumType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, loop.GetLocation()));
    }

    /// <summary>
    ///     Matches a foreach loop whose whole body adds the element to a local. The analyzer and
    ///     the fix provider call this one helper.
    /// </summary>
    internal static bool TryMatchLoop(ForEachStatementSyntax loop, out string sumName, out string valueName)
    {
        sumName = string.Empty;
        valueName = loop.Identifier.ValueText;

        if (loop.Statement is not BlockSyntax body
            || body.Statements.Count != 1
            || body.Statements[0] is not ExpressionStatementSyntax statement
            || statement.Expression is not AssignmentExpressionSyntax add
            || !add.IsKind(SyntaxKind.AddAssignmentExpression)
            || add.Left is not IdentifierNameSyntax sum
            || add.Right is not IdentifierNameSyntax value
            || !string.Equals(value.Identifier.ValueText, valueName, StringComparison.Ordinal))
        {
            return false;
        }

        sumName = sum.Identifier.ValueText;
        return true;
    }

    internal static ITypeSymbol? GetElementType(
        SemanticModel model,
        ExpressionSyntax expression,
        SumContext sumContext,
        System.Threading.CancellationToken cancellationToken)
    {
        return model.GetTypeInfo(expression, cancellationToken).Type switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { TypeArguments.Length: 1 } named
                when SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, sumContext.ReadOnlySpan)
                    || SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, sumContext.Span) =>
                named.TypeArguments[0],
            _ => null
        };
    }

    internal static ITypeSymbol? FindType(
        SemanticModel model,
        ForEachStatementSyntax loop,
        string name,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var node in loop.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (string.Equals(node.Identifier.ValueText, name, StringComparison.Ordinal)
                && model.GetSymbolInfo(node, cancellationToken).Symbol is ILocalSymbol or IParameterSymbol)
            {
                return model.GetTypeInfo(node, cancellationToken).Type;
            }
        }

        return null;
    }

    private static bool HasSum(INamedTypeSymbol tensors, INamedTypeSymbol spanType)
    {
        return tensors.GetMembers("Sum")
            .OfType<IMethodSymbol>()
            .Any(method => method.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, spanType));
    }

    internal sealed class SumContext
    {
        public SumContext(
            ImmutableArray<ITypeSymbol> supportedTypes,
            INamedTypeSymbol readOnlySpan,
            INamedTypeSymbol span)
        {
            SupportedTypes = supportedTypes;
            ReadOnlySpan = readOnlySpan;
            Span = span;
        }

        public ImmutableArray<ITypeSymbol> SupportedTypes { get; }

        public INamedTypeSymbol ReadOnlySpan { get; }

        public INamedTypeSymbol Span { get; }
    }
}
