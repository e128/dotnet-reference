using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LinearLookupInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128066";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Linear lookup inside loop creates O(n²) complexity",
        "'{0}' on '{1}' inside a loop is O(n) per iteration — use a HashSet<T> or Dictionary<K,V> for O(1) lookup",
        "Performance",
        DiagnosticSeverity.Warning,
        true);

    private static readonly ImmutableHashSet<string> LinearMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Contains",
        "Any",
        "IndexOf",
        "Remove",
        "Exists",
        "Find",
        "FindAll",
        "FindIndex");

    private static readonly ImmutableArray<string> O1MetadataNames =
    [
        "System.Collections.Generic.ISet`1",
        "System.Collections.Generic.IReadOnlySet`1",
        "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.IReadOnlyDictionary`2",
        "System.Collections.Frozen.FrozenSet`1",
        "System.Collections.Frozen.FrozenDictionary`2",
        "System.Collections.Immutable.ImmutableHashSet`1",
        "System.Collections.Immutable.ImmutableDictionary`2"
    ];

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
            var o1Types = ResolveO1Types(compilationContext.Compilation);

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, o1Types),
                SyntaxKind.InvocationExpression);
        });
    }

    private static ImmutableArray<INamedTypeSymbol> ResolveO1Types(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var name in O1MetadataNames)
        {
            var type = compilation.GetTypeByMetadataName(name);
            if (type is not null)
            {
                builder.Add(type);
            }
        }

        return builder.ToImmutable();
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> o1Types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!TryGetMemberAccess(invocation, out var memberAccess, out var methodName))
        {
            return;
        }

        if (!LinearMethods.Contains(methodName) && !IsWhereCountPattern(invocation, memberAccess, methodName))
        {
            return;
        }

        if (!IsInsideLoopOrLinqLambda(invocation))
        {
            return;
        }

        if (IsInsideConstantBoundForLoop(invocation))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        var resolvedSymbol = symbolInfo.Symbol
                             ?? (symbolInfo.CandidateSymbols.Length > 0 ? symbolInfo.CandidateSymbols[0] : null);
        if (resolvedSymbol is not IMethodSymbol)
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (receiverType is null)
        {
            return;
        }

        if (IsO1LookupType(receiverType, o1Types))
        {
            return;
        }

        if (!IsLinearLookupType(receiverType, methodName))
        {
            return;
        }

        var receiverName = GetReceiverName(memberAccess);
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            methodName,
            receiverName));
    }

    private static bool TryGetMemberAccess(
        InvocationExpressionSyntax invocation,
        [NotNullWhen(true)] out MemberAccessExpressionSyntax? memberAccess,
        [NotNullWhen(true)] out string? methodName)
    {
        memberAccess = null;
        methodName = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        memberAccess = access;
        methodName = access.Name.Identifier.ValueText;
        return true;
    }

    private static bool IsWhereCountPattern(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        string methodName)
    {
        return string.Equals(methodName, "Count", StringComparison.Ordinal)
               && !invocation.ArgumentList.Arguments.Any()
               && memberAccess.Expression is InvocationExpressionSyntax innerInvocation
               && innerInvocation.Expression is MemberAccessExpressionSyntax innerAccess
               && string.Equals(innerAccess.Name.Identifier.ValueText, "Where", StringComparison.Ordinal);
    }

    private static bool IsInsideLoopOrLinqLambda(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is ForEachStatementSyntax or ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return true;
            }

            if (current is LambdaExpressionSyntax && IsLinqLambdaArgument(current))
            {
                return true;
            }

            if (current is MethodDeclarationSyntax or ConstructorDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsLinqLambdaArgument(SyntaxNode lambda)
    {
        if (lambda.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } })
        {
            return false;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var name = access.Name.Identifier.ValueText;
        return string.Equals(name, "Where", StringComparison.Ordinal)
               || string.Equals(name, "Select", StringComparison.Ordinal)
               || string.Equals(name, "SelectMany", StringComparison.Ordinal)
               || string.Equals(name, "Any", StringComparison.Ordinal)
               || string.Equals(name, "All", StringComparison.Ordinal)
               || string.Equals(name, "First", StringComparison.Ordinal)
               || string.Equals(name, "FirstOrDefault", StringComparison.Ordinal)
               || string.Equals(name, "Single", StringComparison.Ordinal)
               || string.Equals(name, "SingleOrDefault", StringComparison.Ordinal)
               || string.Equals(name, "Last", StringComparison.Ordinal)
               || string.Equals(name, "LastOrDefault", StringComparison.Ordinal)
               || string.Equals(name, "Count", StringComparison.Ordinal)
               || string.Equals(name, "Exists", StringComparison.Ordinal)
               || string.Equals(name, "ForEach", StringComparison.Ordinal);
    }

    private static bool IsInsideConstantBoundForLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is ForStatementSyntax forStatement)
            {
                return IsConstantBound(forStatement);
            }

            if (current is ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax
                or MethodDeclarationSyntax or ConstructorDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsConstantBound(ForStatementSyntax forStatement)
    {
        return forStatement.Condition is BinaryExpressionSyntax binary
               && (binary.Right is LiteralExpressionSyntax || binary.Left is LiteralExpressionSyntax);
    }

    private static bool IsO1LookupType(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> o1Types)
    {
        foreach (var o1 in o1Types)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, o1))
            {
                return true;
            }

            foreach (var iface in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, o1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsLinearLookupType(ITypeSymbol type, string methodName)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (type.TypeKind == TypeKind.Array)
        {
            return string.Equals(methodName, "Contains", StringComparison.Ordinal)
                   || string.Equals(methodName, "Any", StringComparison.Ordinal);
        }

        foreach (var iface in type.AllInterfaces)
        {
            var name = iface.OriginalDefinition.MetadataName;
            if (string.Equals(name, "IList`1", StringComparison.Ordinal)
                || string.Equals(name, "ICollection`1", StringComparison.Ordinal)
                || string.Equals(name, "IReadOnlyList`1", StringComparison.Ordinal)
                || string.Equals(name, "IReadOnlyCollection`1", StringComparison.Ordinal)
                || string.Equals(name, "IEnumerable`1", StringComparison.Ordinal))
            {
                return true;
            }
        }

        var typeMeta = type.OriginalDefinition.MetadataName;
        return string.Equals(typeMeta, "IEnumerable`1", StringComparison.Ordinal)
               || string.Equals(typeMeta, "ICollection`1", StringComparison.Ordinal)
               || string.Equals(typeMeta, "IList`1", StringComparison.Ordinal)
               || string.Equals(typeMeta, "IReadOnlyList`1", StringComparison.Ordinal)
               || string.Equals(typeMeta, "IReadOnlyCollection`1", StringComparison.Ordinal);
    }

    private static string GetReceiverName(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax inner => inner.Name.Identifier.ValueText,
            _ => memberAccess.Expression.ToString()
        };
    }
}
