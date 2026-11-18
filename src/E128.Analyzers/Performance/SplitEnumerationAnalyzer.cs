using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128231: A <c lang="csharp">string.Split</c> result that only a
///     <c lang="csharp">foreach</c> enumerates allocates one array and one string per part.
/// </summary>
/// <remarks>
///     <c lang="csharp">MemoryExtensions.Split</c> yields <c lang="csharp">Range</c> values over
///     the source string with no array and no part strings. The rule reports the loop and the code
///     fix applies only when every use of the part accepts a span, so a loop that stores a part as
///     a string reports with no fix.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SplitEnumerationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128231";

    private const string StringTypeName = "System.String";

    private const string MemoryExtensionsTypeName = "System.MemoryExtensions";

    private const string ReadOnlySpanTypeName = "System.ReadOnlySpan`1";

    private const string StringSplitOptionsTypeName = "System.StringSplitOptions";

    private const string SplitName = "Split";

    private const int SplitParameterCount = 2;

    private const int SeparatorArgumentCount = 1;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Split result enumerated without a stored array",
        "The foreach enumerates a string.Split result. MemoryExtensions.Split yields range values over the same string with no array or part strings.",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "Enumerate source.AsSpan().Split(separator) and read each part as a slice of the span. " +
        "The fix applies only when every use of the part accepts a span.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            if (CreateTerms(start.Compilation) is not { } terms)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, terms),
                SyntaxKind.ForEachStatement);
        });
    }

    /// <summary>
    ///     Resolves the types that a match needs. Returns null when the compilation has no
    ///     <c lang="csharp">MemoryExtensions.Split</c> remedy.
    /// </summary>
    internal static SplitTerms? CreateTerms(Compilation compilation)
    {
        var stringType = compilation.GetTypeByMetadataName(StringTypeName);
        var memoryExtensions = compilation.GetTypeByMetadataName(MemoryExtensionsTypeName);
        var readOnlySpan = compilation.GetTypeByMetadataName(ReadOnlySpanTypeName);
        var optionsType = compilation.GetTypeByMetadataName(StringSplitOptionsTypeName);
        var rangeType = compilation.GetTypeByMetadataName("System.Range");

        if (stringType is null
            || memoryExtensions is null
            || readOnlySpan is null
            || optionsType is null
            || rangeType is null)
        {
            return null;
        }

        return HasSpanSplit(memoryExtensions, readOnlySpan)
            ? new SplitTerms(stringType, optionsType, rangeType)
            : null;
    }

    private static bool HasSpanSplit(INamedTypeSymbol memoryExtensions, INamedTypeSymbol readOnlySpan)
    {
        return memoryExtensions.GetMembers(SplitName)
            .OfType<IMethodSymbol>()
            .Any(method => method.IsExtensionMethod
                           && method.Parameters.Length is SplitParameterCount
                           && TypeSymbols.MatchesOriginalDefinition(method.Parameters[0].Type, readOnlySpan));
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, SplitTerms terms)
    {
        var loop = (ForEachStatementSyntax)context.Node;

        if (!TryMatch(loop, context.SemanticModel, terms, context.CancellationToken, out _, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, loop.GetLocation()));
    }

    /// <summary>
    ///     Matches a foreach over a <c lang="csharp">string.Split</c> result with a
    ///     <c lang="csharp">char</c> or constant-string separator and no split options.
    /// </summary>
    internal static bool TryMatch(
        ForEachStatementSyntax loop,
        SemanticModel model,
        SplitTerms terms,
        CancellationToken cancellationToken,
        out ExpressionSyntax? receiver,
        out ExpressionSyntax? separator)
    {
        receiver = null;
        separator = null;

        if (!IsStringElement(loop.Type, model, terms, cancellationToken))
        {
            return false;
        }

        if (loop.Expression is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || !string.Equals(access.Name.Identifier.ValueText, SplitName, StringComparison.Ordinal))
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments.Count is not SeparatorArgumentCount)
        {
            return false;
        }

        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || !IsSingleArgumentSplit(method, terms))
        {
            return false;
        }

        var separatorArgument = invocation.ArgumentList.Arguments[0].Expression;

        if (!IsSupportedSeparator(method.Parameters[0].Type, separatorArgument, model, cancellationToken))
        {
            return false;
        }

        receiver = access.Expression;
        separator = separatorArgument;
        return true;
    }

    private static bool IsSingleArgumentSplit(IMethodSymbol method, SplitTerms terms)
    {
        return TypeSymbols.Matches(method.ContainingType, terms.StringType)
               && method.Parameters.Length is SplitParameterCount
               && TypeSymbols.Matches(method.Parameters[1].Type, terms.OptionsType)
               && method.Parameters[1].ExplicitDefaultValue is 0;
    }

    private static bool IsSupportedSeparator(
        ITypeSymbol parameterType,
        ExpressionSyntax separatorArgument,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (parameterType.SpecialType is SpecialType.System_Char)
        {
            return true;
        }

        if (parameterType.SpecialType is not SpecialType.System_String)
        {
            return false;
        }

        var constant = model.GetConstantValue(separatorArgument, cancellationToken);

        return constant.HasValue && constant.Value is string;
    }

    private static bool IsStringElement(
        ExpressionSyntax type,
        SemanticModel model,
        SplitTerms terms,
        CancellationToken cancellationToken)
    {
        if (type is IdentifierNameSyntax identifier
            && string.Equals(identifier.Identifier.ValueText, "var", StringComparison.Ordinal))
        {
            return true;
        }

        return TypeSymbols.Matches(model.GetTypeInfo(type, cancellationToken).Type, terms.StringType);
    }

    /// <summary>
    ///     True when the receiver is a name chain, so the fix can repeat it without a second call.
    /// </summary>
    internal static bool IsStableReceiver(ExpressionSyntax receiver)
    {
        return receiver switch
        {
            IdentifierNameSyntax => true,
            MemberAccessExpressionSyntax access => access.Expression
                is IdentifierNameSyntax or ThisExpressionSyntax or BaseExpressionSyntax,
            _ => false
        };
    }

    /// <summary>
    ///     Collects every read of the loop variable and checks that the rewrite keeps its meaning.
    /// </summary>
    internal static bool CanRewrite(
        ForEachStatementSyntax loop,
        SemanticModel model,
        SplitTerms terms,
        ExpressionSyntax receiver,
        CancellationToken cancellationToken,
        out ImmutableArray<IdentifierNameSyntax> reads)
    {
        reads = [];

        if (!IsStableReceiver(receiver)
            || model.GetDeclaredSymbol(loop, cancellationToken) is not ILocalSymbol variable
            || model.GetSymbolInfo(receiver, cancellationToken).Symbol is not (ILocalSymbol or IParameterSymbol or IFieldSymbol))
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<IdentifierNameSyntax>();

        foreach (var identifier in loop.Statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (!string.Equals(identifier.Identifier.ValueText, loop.Identifier.ValueText, StringComparison.Ordinal))
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    variable))
            {
                continue;
            }

            if (!IsEligibleUse(identifier, model, terms, cancellationToken))
            {
                return false;
            }

            builder.Add(identifier);
        }

        reads = builder.ToImmutable();
        return true;
    }

    private static bool IsEligibleUse(
        IdentifierNameSyntax identifier,
        SemanticModel model,
        SplitTerms terms,
        CancellationToken cancellationToken)
    {
        switch (identifier.Parent)
        {
            case MemberAccessExpressionSyntax access when access.Expression.Span == identifier.Span:
                return string.Equals(access.Name.Identifier.ValueText, "Length", StringComparison.Ordinal);

            case ElementAccessExpressionSyntax element when element.Expression.Span == identifier.Span:
                return element.ArgumentList.Arguments.Count is 1
                       && !TypeSymbols.Matches(
                           model.GetTypeInfo(element.ArgumentList.Arguments[0].Expression, cancellationToken).Type,
                           terms.RangeType);

            case BinaryExpressionSyntax comparison
                when comparison.IsKind(SyntaxKind.EqualsExpression) || comparison.IsKind(SyntaxKind.NotEqualsExpression):
                return ComparesWithString(comparison, identifier, model, cancellationToken);

            default:
                return false;
        }
    }

    private static bool ComparesWithString(
        BinaryExpressionSyntax comparison,
        IdentifierNameSyntax identifier,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var other = comparison.Left.Span == identifier.Span ? comparison.Right : comparison.Left;
        var constant = model.GetConstantValue(other, cancellationToken);

        return constant.HasValue && constant.Value is string;
    }

    /// <summary>
    ///     The resolved types that one match needs.
    /// </summary>
    internal sealed class SplitTerms
    {
        public SplitTerms(
            INamedTypeSymbol stringType,
            INamedTypeSymbol optionsType,
            INamedTypeSymbol rangeType)
        {
            StringType = stringType;
            OptionsType = optionsType;
            RangeType = rangeType;
        }

        public INamedTypeSymbol StringType { get; }

        public INamedTypeSymbol OptionsType { get; }

        public INamedTypeSymbol RangeType { get; }
    }
}
