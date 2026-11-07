using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128180: Flags a materializing Entity Framework Core query inside a loop body. The query runs
///     once per iteration, so a loop over N items sends N database round trips.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EfQueryInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128180";

    private const string DbSetMetadataName = "Microsoft.EntityFrameworkCore.DbSet`1";

    private const string QueryableMetadataName = "System.Linq.IQueryable`1";

    private const string EfNamespace = "Microsoft.EntityFrameworkCore";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "EF Core query executed inside loop",
        "'{0}' inside a loop runs one database round trip per iteration: move the query outside the loop or batch it",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "An Entity Framework Core query that executes inside a for, foreach, while, or do body sends one database round trip per iteration. Hoist the query and filter the rows in memory, or batch the work into one call.");

    private static readonly ImmutableHashSet<string> MaterializingMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ToList",
        "ToListAsync",
        "ToArray",
        "ToArrayAsync",
        "First",
        "FirstAsync",
        "FirstOrDefault",
        "FirstOrDefaultAsync",
        "Single",
        "SingleAsync",
        "SingleOrDefault",
        "SingleOrDefaultAsync",
        "Count",
        "CountAsync",
        "Any",
        "AnyAsync",
        "All",
        "AllAsync",
        "Sum",
        "SumAsync",
        "Max",
        "MaxAsync",
        "Min",
        "MinAsync",
        "Find",
        "FindAsync",
        "Contains",
        "ContainsAsync",
        "Last",
        "LastAsync",
        "LastOrDefault",
        "LastOrDefaultAsync",
        "ElementAt",
        "ElementAtAsync",
        "ElementAtOrDefault",
        "ElementAtOrDefaultAsync",
        "Average",
        "AverageAsync",
        "LongCount",
        "LongCountAsync",
        "ToDictionary",
        "ToDictionaryAsync",
        "ToHashSet",
        "ToHashSetAsync",
        "Load",
        "LoadAsync",
        "ExecuteDelete",
        "ExecuteDeleteAsync",
        "ExecuteUpdate",
        "ExecuteUpdateAsync");

    // These calls do not run a query. They report only as the source of a foreach that itself runs
    // inside an outer loop, because that foreach enumerates the query once per outer iteration.
    private static readonly ImmutableHashSet<string> DeferredMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "AsEnumerable",
        "AsAsyncEnumerable");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        // Without DbSet<T> the code has no Entity Framework Core query source, so the rule stays silent.
        var dbSetDefinitions = MetadataTypes.Find(context.Compilation, DbSetMetadataName);
        var queryableDefinitions = MetadataTypes.Find(context.Compilation, QueryableMetadataName);

        if (dbSetDefinitions.IsEmpty || queryableDefinitions.IsEmpty)
        {
            return;
        }

        var known = new KnownTypes(dbSetDefinitions, queryableDefinitions[0]);
        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeInvocation(nodeContext, known),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, KnownTypes known)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        var isDeferred = DeferredMethodNames.Contains(methodName);

        if (!isDeferred && !MaterializingMethodNames.Contains(methodName))
        {
            return;
        }

        if (isDeferred)
        {
            if (!FeedsNestedForEachSource(invocation))
            {
                return;
            }
        }
        else if (!LoopContext.IsInsideLoopBody(invocation))
        {
            return;
        }

        // An unresolved call has no confirmed binding, so an editor buffer mid-edit produces no report.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol)
        {
            return;
        }

        if (!IsEfQuerySource(memberAccess.Expression, context.SemanticModel, known, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }

    /// <summary>
    ///     True when the call is the source of a foreach that runs inside another loop. The deferred
    ///     call itself does no work, so only the per-iteration enumeration is a round trip.
    /// </summary>
    private static bool FeedsNestedForEachSource(InvocationExpressionSyntax invocation)
    {
        SyntaxNode current = invocation;

        while (current.Parent is ParenthesizedExpressionSyntax)
        {
            current = current.Parent;
        }

        return current.Parent is CommonForEachStatementSyntax forEach
               && ReferenceEquals(forEach.Expression, current)
               && LoopContext.IsInsideLoopBody(forEach);
    }

    private static bool IsEfQuerySource(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        KnownTypes known,
        CancellationToken cancellationToken)
    {
        return semanticModel.GetTypeInfo(expression, cancellationToken).Type is { } type
               && (IsEfQueryType(type, known) || (IsQueryable(type, known) && HasEfRoot(expression, semanticModel, known, cancellationToken)));
    }

    private static bool IsEfQueryType(ITypeSymbol type, KnownTypes known)
    {
        return IsDbSet(type, known.DbSetDefinitions) || (IsQueryable(type, known) && IsInEfNamespace(type));
    }

    private static bool IsDbSet(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> definitions)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (MetadataTypes.Contains(definitions, current.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsQueryable(ITypeSymbol type, KnownTypes known)
    {
        if (HasQueryableInterface(type, known.Queryable))
        {
            return true;
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (HasQueryableInterface(implemented, known.Queryable))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasQueryableInterface(ITypeSymbol type, INamedTypeSymbol queryable)
    {
        return type is INamedTypeSymbol named
               && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, queryable);
    }

    private static bool IsInEfNamespace(ITypeSymbol type)
    {
        var namespaceName = type.ContainingNamespace?.ToDisplayString();

        return namespaceName is not null
               && (string.Equals(namespaceName, EfNamespace, StringComparison.Ordinal)
                   || namespaceName.StartsWith(EfNamespace + ".", StringComparison.Ordinal));
    }

    private static bool HasEfRoot(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        KnownTypes known,
        CancellationToken cancellationToken)
    {
        var current = expression;
        while (current is not null)
        {
            if (semanticModel.GetTypeInfo(current, cancellationToken).Type is { } type && IsEfQueryType(type, known))
            {
                return true;
            }

            current = Unwrap(current);
        }

        return false;
    }

    private static ExpressionSyntax? Unwrap(ExpressionSyntax expression)
    {
        var stripped = ExpressionUnwrapper.ParenthesesAndCasts(expression);

        return ReferenceEquals(stripped, expression)
            ? expression switch
            {
                InvocationExpressionSyntax invocation => ExpressionUnwrapper.Receiver(invocation),
                MemberAccessExpressionSyntax access => access.Expression,
                QueryExpressionSyntax query => query.FromClause.Expression,
                _ => null
            }
            : stripped;
    }

    private sealed class KnownTypes
    {
        internal KnownTypes(ImmutableArray<INamedTypeSymbol> dbSetDefinitions, INamedTypeSymbol queryable)
        {
            DbSetDefinitions = dbSetDefinitions;
            Queryable = queryable;
        }

        internal ImmutableArray<INamedTypeSymbol> DbSetDefinitions { get; }

        internal INamedTypeSymbol Queryable { get; }
    }
}
