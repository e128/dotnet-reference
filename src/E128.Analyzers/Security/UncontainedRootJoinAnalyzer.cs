using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128222: Reports a <c lang="csharp">Path.Combine(root.FullName, leaf)</c> call whose leaf
///     arrives at run time, when no containment check in the same method consumes the join.
/// </summary>
/// <remarks>
///     The rule is a security rule, and a missed report is the costly failure. A guard call
///     silences the report only when it receives the join itself, or a local or field that the
///     join reaches through an assignment. An unrelated guard call in the same method does not
///     silence it. Constant leaves and names returned by the namer family stay silent, because
///     they cannot carry a separator or a parent reference. E128161 covers a constant segment
///     that is rooted or climbs. This rule covers the leaf that arrives at run time.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UncontainedRootJoinAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128222";

    private const string CombineMethodName = "Combine";

    private const int JoinArgumentCount = 2;

    private const string FullNamePropertyName = "FullName";

    private const string PathTypeName = "System.IO.Path";

    // One source for the guard family. A call with one of these names consumes a path value.
    private static readonly HashSet<string> GuardMethodNames = new(StringComparer.Ordinal)
    {
        "IsInside",
        "ValidateDomainPath",
        "ResolveInside"
    };

    // One source for the numeric hole types. Their text holds digits, a sign, or a decimal point.
    private static readonly HashSet<SpecialType> NumericSpecialTypes =
    [
        SpecialType.System_Byte,
        SpecialType.System_SByte,
        SpecialType.System_Int16,
        SpecialType.System_UInt16,
        SpecialType.System_Int32,
        SpecialType.System_UInt32,
        SpecialType.System_Int64,
        SpecialType.System_UInt64,
        SpecialType.System_Single,
        SpecialType.System_Double,
        SpecialType.System_Decimal
    ];

    // One source for the name producers. A leaf from one of these calls holds a file name only.
    private static readonly HashSet<string> NamerMethodNames = new(StringComparer.Ordinal)
    {
        "GetFileName",
        "GetFileNameWithoutExtension",
        "GetRandomFileName",
        "GetTempFileName",
        "ChangeExtension",
        "SanitizeFileName",
        "ToKebabCase"
    };

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Path.Combine with a run-time leaf and no containment check",
        "Path.Combine joins a run-time value to '{0}' without a containment check. Call FilePathContainment.ResolveInside.",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A leaf that a caller supplies at run time can hold a path separator, a parent reference, " +
        "or a rooted prefix. Path.Combine then returns a path outside the root directory. " +
        "FilePathContainment.ResolveInside resolves the join and proves the result stays inside " +
        "the root before the caller reads or deletes the file.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.ArgumentList.Arguments.Count is not JoinArgumentCount
            || invocation.Expression is not MemberAccessExpressionSyntax combineAccess
            || !string.Equals(
                combineAccess.Name.Identifier.ValueText,
                CombineMethodName,
                StringComparison.Ordinal))
        {
            return;
        }

        if (!IsSystemPathCombine(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (!IsRootFullName(invocation.ArgumentList.Arguments[0].Expression))
        {
            return;
        }

        var leaf = invocation.ArgumentList.Arguments[1].Expression;
        if (IsSafeLeaf(leaf, context))
        {
            return;
        }

        if (IsConsumedByGuard(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), leaf.ToString()));
    }

    private static bool IsSystemPathCombine(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
               && string.Equals(method.Name, CombineMethodName, StringComparison.Ordinal)
               && string.Equals(
                   method.ContainingType?.ToDisplayString(),
                   PathTypeName,
                   StringComparison.Ordinal);
    }

    private static bool IsRootFullName(ExpressionSyntax expression)
    {
        var value = UnwrapParentheses(expression);

        return value is MemberAccessExpressionSyntax { Name: { } name }
               && string.Equals(name.Identifier.ValueText, FullNamePropertyName, StringComparison.Ordinal);
    }

    private static bool IsSafeLeaf(ExpressionSyntax leaf, SyntaxNodeAnalysisContext context)
    {
        var constant = context.SemanticModel.GetConstantValue(leaf, context.CancellationToken);
        if (constant.HasValue && constant.Value is string)
        {
            return true;
        }

        if (IsNamerInvocation(leaf))
        {
            return true;
        }

        return leaf is InterpolatedStringExpressionSyntax interpolated
               && HasOnlyNumericHoles(interpolated, context.SemanticModel, context.CancellationToken);
    }

    private static bool IsNamerInvocation(ExpressionSyntax leaf)
    {
        var value = UnwrapParentheses(leaf);

        return value is InvocationExpressionSyntax invocation
               && GetInvokedName(invocation) is { } name
               && NamerMethodNames.Contains(name);
    }

    private static bool HasOnlyNumericHoles(
        InterpolatedStringExpressionSyntax interpolated,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var content in interpolated.Contents)
        {
            // The literal parts of the format string carry the traversal. $"../{index}.txt" has
            // one numeric hole and still climbs, so a separator or a parent reference in the
            // text ends the numeric-only case.
            if (content is InterpolatedStringTextSyntax text && CarriesPathSyntax(text.TextToken.ValueText))
            {
                return false;
            }

            if (content is not InterpolationSyntax interpolation)
            {
                continue;
            }

            var type = model.GetTypeInfo(interpolation.Expression, cancellationToken).Type;
            if (type is null || !IsNumericType(type))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CarriesPathSyntax(string text)
    {
        return text.IndexOf('/') >= 0
               || text.IndexOf('\\') >= 0
               || text.IndexOf("..", StringComparison.Ordinal) >= 0;
    }

    private static bool IsNumericType(ITypeSymbol type)
    {
        return NumericSpecialTypes.Contains(type.SpecialType);
    }

    /// <summary>
    ///     True when a guard call in the same scope receives the join, or a local or field that
    ///     the join reaches. A guard call that receives any other value leaves the join exposed.
    /// </summary>
    private static bool IsConsumedByGuard(InvocationExpressionSyntax join)
    {
        var scope = FindScope(join);
        if (scope is null)
        {
            return false;
        }

        var boundNames = CollectBoundNames(join, scope);

        foreach (var node in scope.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax call
                && IsGuardCall(call)
                && ConsumesJoin(call, join, boundNames))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ConsumesJoin(
        InvocationExpressionSyntax guard,
        InvocationExpressionSyntax join,
        HashSet<string> boundNames)
    {
        foreach (var argument in guard.ArgumentList.Arguments)
        {
            var value = Unwrap(argument.Expression);

            if (ReferenceEquals(value, join))
            {
                return true;
            }

            if (value is IdentifierNameSyntax identifier
                && boundNames.Contains(identifier.Identifier.ValueText))
            {
                return true;
            }

            if (value is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: { } memberName }
                && boundNames.Contains(memberName.Identifier.ValueText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Collects every local or field name that the join reaches through an assignment. The
    ///     scan repeats until no new name appears, so a chain of assignments is included.
    /// </summary>
    private static HashSet<string> CollectBoundNames(InvocationExpressionSyntax join, SyntaxNode scope)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        bool added;

        do
        {
            added = false;

            foreach (var node in scope.DescendantNodes())
            {
                added |= TryCollectBoundName(node, join, names);
            }
        }
        while (added);

        return names;
    }

    private static bool TryCollectBoundName(
        SyntaxNode node,
        InvocationExpressionSyntax join,
        HashSet<string> boundNames)
    {
        switch (node)
        {
            case EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } clause:
                return Binds(clause.Value, join, boundNames)
                       && boundNames.Add(declarator.Identifier.ValueText);

            case EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } clause:
                return Binds(clause.Value, join, boundNames)
                       && boundNames.Add(property.Identifier.ValueText);

            case AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } assignment:
                return Binds(assignment.Right, join, boundNames)
                       && boundNames.Add(left.Identifier.ValueText);

            case AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax { Name: { } name } } assignment:
                return Binds(assignment.Right, join, boundNames)
                       && boundNames.Add(name.Identifier.ValueText);

            default:
                return false;
        }
    }

    private static bool Binds(
        ExpressionSyntax value,
        InvocationExpressionSyntax join,
        HashSet<string> boundNames)
    {
        var core = Unwrap(value);

        if (ReferenceEquals(core, join))
        {
            return true;
        }

        return core is IdentifierNameSyntax identifier
               && boundNames.Contains(identifier.Identifier.ValueText);
    }

    private static SyntaxNode? FindScope(InvocationExpressionSyntax join)
    {
        foreach (var ancestor in join.Ancestors())
        {
            switch (ancestor)
            {
                case LocalFunctionStatementSyntax:
                case AnonymousFunctionExpressionSyntax:
                case MethodDeclarationSyntax:
                case ConstructorDeclarationSyntax:
                case DestructorDeclarationSyntax:
                case AccessorDeclarationSyntax:
                case TypeDeclarationSyntax:
                case CompilationUnitSyntax:
                    return ancestor;

                default:
                    continue;
            }
        }

        return null;
    }

    private static bool IsGuardCall(InvocationExpressionSyntax call)
    {
        return GetInvokedName(call) is { } name && GuardMethodNames.Contains(name);
    }

    private static string? GetInvokedName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => null
        };
    }

    /// <summary>
    ///     Reduces an argument to the value it carries. The walk removes parentheses and a single
    ///     construction, so a guard call that receives <c lang="csharp">new FileInfo(join)</c>
    ///     reads as a guard call that receives the join.
    /// </summary>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var value = UnwrapParentheses(expression);

        while (true)
        {
            switch (value)
            {
                case BaseObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } creation:
                    value = UnwrapParentheses(creation.ArgumentList.Arguments[0].Expression);
                    continue;

                default:
                    return value;
            }
        }
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        var value = expression;

        while (value is ParenthesizedExpressionSyntax parenthesized)
        {
            value = parenthesized.Expression;
        }

        return value;
    }
}
