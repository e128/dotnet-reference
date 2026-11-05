using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128184: Reports a recursive directory delete whose target path the rule cannot prove safe.
///     A path built from a parameter, a field, or a property can point outside the intended root.
/// </summary>
/// <remarks>
///     The rule reports only a shape it can prove unsafe, and it never follows a path across a
///     method boundary. A path rooted at a temp directory, a literal, a proven
///     <c lang="csharp">Path.GetFullPath</c> result, or a value guarded by a
///     <c lang="csharp">StartsWith</c> check stays silent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RecursiveDeleteRootAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128184";

    private const string DirectoryMetadataName = "System.IO.Directory";
    private const string DirectoryInfoMetadataName = "System.IO.DirectoryInfo";
    private const string PathMetadataName = "System.IO.Path";
    private const string DeleteMethodName = "Delete";
    private const string CombineMethodName = "Combine";
    private const string GetFullPathMethodName = "GetFullPath";
    private const string GetTempPathMethodName = "GetTempPath";
    private const string StartsWithMethodName = "StartsWith";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Recursive delete outside a proven safe root",
        "'{0}' is deleted recursively, and the rule cannot prove the path stays inside a temp directory or a fixed root",
        "E128-Security",
        DiagnosticSeverity.Info,
        true,
        "A recursive delete of an unproven path can remove files outside the intended root. Build the path from Path.GetTempPath(), use a string literal, or guard it with StartsWith against a fixed root.");

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
            // The rule names two System.IO types. When either is absent the members below belong
            // to some other type, so the rule must not run.
            var directoryTypes = MetadataTypes.Find(startContext.Compilation, DirectoryMetadataName);
            var directoryInfoTypes = MetadataTypes.Find(startContext.Compilation, DirectoryInfoMetadataName);
            if (directoryTypes.IsEmpty || directoryInfoTypes.IsEmpty)
            {
                return;
            }

            var pathTypes = MetadataTypes.Find(startContext.Compilation, PathMetadataName);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, directoryTypes, directoryInfoTypes, pathTypes),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> directoryTypes,
        ImmutableArray<INamedTypeSymbol> directoryInfoTypes,
        ImmutableArray<INamedTypeSymbol> pathTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        if (!TryGetDeleteArguments(
                invocation,
                model,
                directoryTypes,
                directoryInfoTypes,
                cancellationToken,
                out var path,
                out var recursive))
        {
            return;
        }

        if (model.GetConstantValue(recursive, cancellationToken).Value is not true)
        {
            return;
        }

        if (IsGuarded(invocation, path, model, pathTypes, cancellationToken))
        {
            return;
        }

        if (ClassifyPath(path, model, pathTypes, cancellationToken) != PathSafety.Unsafe)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, invocation.GetLocation(), DisplayName(path, model, cancellationToken)));
    }

    /// <summary>
    ///     Finds the path and recursive arguments of one of the supported delete overloads.
    /// </summary>
    private static bool TryGetDeleteArguments(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        ImmutableArray<INamedTypeSymbol> directoryTypes,
        ImmutableArray<INamedTypeSymbol> directoryInfoTypes,
        CancellationToken cancellationToken,
        out ExpressionSyntax path,
        out ExpressionSyntax recursive)
    {
        path = invocation;
        recursive = invocation;

        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || !string.Equals(method.Name, DeleteMethodName, StringComparison.Ordinal))
        {
            return false;
        }

        if (MetadataTypes.Contains(directoryTypes, method.ContainingType))
        {
            // Directory.Delete(string, bool) and Directory.Delete(string, bool, bool). The recursive
            // flag is the second parameter on both overloads.
            if (method.Parameters.Length < 2
                || method.Parameters[1].Type.SpecialType != SpecialType.System_Boolean)
            {
                return false;
            }

            var pathArgument = FindArgument(invocation, method, parameterIndex: 0);
            var recursiveArgument = FindArgument(invocation, method, parameterIndex: 1);
            if (pathArgument is null || recursiveArgument is null)
            {
                return false;
            }

            path = pathArgument;
            recursive = recursiveArgument;
            return true;
        }

        if (MetadataTypes.Contains(directoryInfoTypes, method.ContainingType))
        {
            // DirectoryInfo.Delete(bool). The instance owns the path.
            if (method.Parameters.Length != 1
                || method.Parameters[0].Type.SpecialType != SpecialType.System_Boolean
                || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                return false;
            }

            var recursiveArgument = FindArgument(invocation, method, parameterIndex: 0);
            if (recursiveArgument is null)
            {
                return false;
            }

            path = memberAccess.Expression;
            recursive = recursiveArgument;
            return true;
        }

        return false;
    }

    private static ExpressionSyntax? FindArgument(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        int parameterIndex)
    {
        var parameter = method.Parameters[parameterIndex];
        var positionalIndex = 0;

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.NameColon is { } nameColon)
            {
                if (string.Equals(
                        nameColon.Name.Identifier.ValueText,
                        parameter.Name,
                        StringComparison.Ordinal))
                {
                    return argument.Expression;
                }

                continue;
            }

            if (positionalIndex == parameterIndex)
            {
                return argument.Expression;
            }

            positionalIndex++;
        }

        return null;
    }

    /// <summary>
    ///     Decides whether the path provably stays inside a temp directory or a fixed root.
    /// </summary>
    private static PathSafety ClassifyPath(
        ExpressionSyntax expression,
        SemanticModel model,
        ImmutableArray<INamedTypeSymbol> pathTypes,
        CancellationToken cancellationToken)
    {
        var unwrapped = ExpressionUnwrapper.ParenthesesAndCasts(expression);

        // A filesystem root holds files that other programs own.
        if (IsFilesystemRoot(unwrapped, model, cancellationToken))
        {
            return PathSafety.Unsafe;
        }

        if (IsFixedRoot(unwrapped, model, cancellationToken)
            || IsTempRooted(unwrapped, model, pathTypes, cancellationToken))
        {
            return PathSafety.Safe;
        }

        if (FindPathInvocation(unwrapped, GetFullPathMethodName, pathTypes, model, cancellationToken)
            is { } getFullPath)
        {
            var arguments = getFullPath.ArgumentList.Arguments;
            return arguments.Count > 0
                ? ClassifyPath(arguments[0].Expression, model, pathTypes, cancellationToken)
                : PathSafety.Unknown;
        }

        if (FindPathInvocation(unwrapped, CombineMethodName, pathTypes, model, cancellationToken)
            is { } combine)
        {
            var arguments = combine.ArgumentList.Arguments;
            if (arguments.Count == 0)
            {
                return PathSafety.Unknown;
            }

            var first = arguments[0].Expression;
            var isSafe = IsFixedRoot(first, model, cancellationToken)
                         || IsTempRooted(first, model, pathTypes, cancellationToken);
            var isUnsafe = !isSafe && IsParameterOrMemberRead(first, model, cancellationToken);

            return (isSafe, isUnsafe) switch
            {
                (true, _) => PathSafety.Safe,
                (_, true) => PathSafety.Unsafe,
                _ => PathSafety.Unknown
            };
        }

        return IsParameterOrMemberRead(unwrapped, model, cancellationToken)
            ? PathSafety.Unsafe
            : PathSafety.Unknown;
    }

    // A constant that names a drive root or the Unix root, such as "/", "\\", "C:\\" or "C:/".
    private static bool IsFilesystemRoot(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (model.GetConstantValue(expression, cancellationToken).Value is not string value)
        {
            return false;
        }

        var trimmed = value.Trim();

        return trimmed is "/" or "\\"
               || (trimmed.Length is 2 or 3
                   && char.IsLetter(trimmed[0])
                   && trimmed[1] == ':'
                   && (trimmed.Length == 2 || trimmed[2] is '\\' or '/'));
    }

    private static bool IsTempRooted(
        ExpressionSyntax expression,
        SemanticModel model,
        ImmutableArray<INamedTypeSymbol> pathTypes,
        CancellationToken cancellationToken)
    {
        var unwrapped = ExpressionUnwrapper.ParenthesesAndCasts(expression);

        if (FindPathInvocation(unwrapped, GetTempPathMethodName, pathTypes, model, cancellationToken) is not null)
        {
            return true;
        }

        if (FindPathInvocation(unwrapped, CombineMethodName, pathTypes, model, cancellationToken)
                is { } combine
            && combine.ArgumentList.Arguments.Count > 0)
        {
            return IsTempRooted(
                combine.ArgumentList.Arguments[0].Expression,
                model,
                pathTypes,
                cancellationToken);
        }

        var getFullPath = FindPathInvocation(
            unwrapped,
            GetFullPathMethodName,
            pathTypes,
            model,
            cancellationToken);

        return getFullPath is not null
               && getFullPath.ArgumentList.Arguments.Count > 0
               && IsTempRooted(
                   getFullPath.ArgumentList.Arguments[0].Expression,
                   model,
                   pathTypes,
                   cancellationToken);
    }

    private static bool IsFixedRoot(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var unwrapped = ExpressionUnwrapper.ParenthesesAndCasts(expression);

        var isLiteral = unwrapped is LiteralExpressionSyntax literal
                        && literal.IsKind(SyntaxKind.StringLiteralExpression);

        return isLiteral
               || (model.GetSymbolInfo(unwrapped, cancellationToken).Symbol
                       is IFieldSymbol { IsConst: true } field
                   && field.Type.SpecialType == SpecialType.System_String);
    }

    private static bool IsParameterOrMemberRead(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var unwrapped = ExpressionUnwrapper.ParenthesesAndCasts(expression);

        return model.GetSymbolInfo(unwrapped, cancellationToken).Symbol
               is IParameterSymbol or IFieldSymbol or IPropertySymbol;
    }

    private static InvocationExpressionSyntax? FindPathInvocation(
        ExpressionSyntax expression,
        string methodName,
        ImmutableArray<INamedTypeSymbol> pathTypes,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return expression is InvocationExpressionSyntax invocation
               && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
               && string.Equals(method.Name, methodName, StringComparison.Ordinal)
               && MetadataTypes.Contains(pathTypes, method.ContainingType)
            ? invocation
            : null;
    }

    /// <summary>
    ///     Returns true when an if in the same block or an enclosing if tests the deleted path with
    ///     <c lang="csharp">StartsWith</c> against a fixed root.
    /// </summary>
    private static bool IsGuarded(
        InvocationExpressionSyntax deleteInvocation,
        ExpressionSyntax path,
        SemanticModel model,
        ImmutableArray<INamedTypeSymbol> pathTypes,
        CancellationToken cancellationToken)
    {
        foreach (var (ifStatement, encloses) in CandidateGuards(deleteInvocation))
        {
            foreach (var invocation in ifStatement.Condition.DescendantNodesAndSelf()
                         .OfType<InvocationExpressionSyntax>())
            {
                if (FindStartsWithSubject(invocation, model, cancellationToken) is not { } subject)
                {
                    continue;
                }

                if (!ProvesStartsWithTrue(invocation, ifStatement, encloses))
                {
                    continue;
                }

                if (!IsSafeRoot(
                        invocation.ArgumentList.Arguments.Count > 0
                            ? invocation.ArgumentList.Arguments[0].Expression
                            : null,
                        model,
                        pathTypes,
                        cancellationToken))
                {
                    continue;
                }

                if (IsSameValue(subject, path, model, pathTypes, cancellationToken))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     True when the delete can run only after the StartsWith call returned true. An enclosing
    ///     then branch needs a condition that holds only when the call is true: the call alone, or
    ///     joined by <c lang="csharp">&amp;&amp;</c>. A preceding if must end in an exit and must
    ///     have no else branch. Its condition holds when the call is false, so the call appears
    ///     under one <c lang="csharp">!</c>, joined by <c lang="csharp">||</c>.
    /// </summary>
    private static bool ProvesStartsWithTrue(
        InvocationExpressionSyntax startsWith,
        IfStatementSyntax ifStatement,
        bool encloses)
    {
        if (!encloses && (ifStatement.Else is not null || !StatementExits.EndsInExit(ifStatement.Statement)))
        {
            return false;
        }

        var allowedBinary = encloses ? SyntaxKind.LogicalAndExpression : SyntaxKind.LogicalOrExpression;
        var seenNot = false;
        SyntaxNode node = startsWith;

        while (!ReferenceEquals(node, ifStatement.Condition))
        {
            var parent = node.Parent;

            if (parent is ParenthesizedExpressionSyntax)
            {
                // Parentheses never change the value.
            }
            else if (parent is PrefixUnaryExpressionSyntax prefix
                     && prefix.IsKind(SyntaxKind.LogicalNotExpression)
                     && !encloses
                     && !seenNot)
            {
                seenNot = true;
            }
            else if (parent is BinaryExpressionSyntax binary
                     && binary.IsKind(allowedBinary)
                     && (encloses || seenNot))
            {
                // The other operand of this operator does not change what the call proves.
            }
            else
            {
                return false;
            }

            node = parent;
        }

        return encloses || seenNot;
    }

    private static IEnumerable<(IfStatementSyntax If, bool Encloses)> CandidateGuards(SyntaxNode deleteInvocation)
    {
        for (var current = deleteInvocation.Parent; current is not null; current = current.Parent)
        {
            // A guard that encloses the delete proves the path only in its then branch. An else
            // branch runs when the test failed, so it is not a guard.
            if (current is IfStatementSyntax ancestor
                && ancestor.Statement.Span.Contains(deleteInvocation.Span))
            {
                yield return (ancestor, true);
            }

            if (current is BlockSyntax block)
            {
                foreach (var statement in block.Statements)
                {
                    if (statement.SpanStart >= deleteInvocation.SpanStart)
                    {
                        break;
                    }

                    if (statement.Span.Contains(deleteInvocation.Span))
                    {
                        continue;
                    }

                    if (statement is IfStatementSyntax preceding)
                    {
                        yield return (preceding, false);
                    }
                }
            }

            if (current is MemberDeclarationSyntax)
            {
                break;
            }
        }
    }

    private static ExpressionSyntax? FindStartsWithSubject(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
               && string.Equals(
                   memberAccess.Name.Identifier.ValueText,
                   StartsWithMethodName,
                   StringComparison.Ordinal)
               && model.GetSymbolInfo(invocation, cancellationToken).Symbol
                   is IMethodSymbol { ContainingType.SpecialType: SpecialType.System_String }
            ? memberAccess.Expression
            : null;
    }

    private static bool IsSafeRoot(
        ExpressionSyntax? expression,
        SemanticModel model,
        ImmutableArray<INamedTypeSymbol> pathTypes,
        CancellationToken cancellationToken)
    {
        if (expression is null)
        {
            return false;
        }

        var unwrapped = ExpressionUnwrapper.ParenthesesAndCasts(expression);
        var getFullPath = FindPathInvocation(
            unwrapped,
            GetFullPathMethodName,
            pathTypes,
            model,
            cancellationToken);

        return IsFixedRoot(unwrapped, model, cancellationToken)
               || IsTempRooted(unwrapped, model, pathTypes, cancellationToken)
               || (getFullPath is not null
                   && getFullPath.ArgumentList.Arguments.Count > 0
                   && IsSafeRoot(
                       getFullPath.ArgumentList.Arguments[0].Expression,
                       model,
                       pathTypes,
                       cancellationToken))
               || model.GetSymbolInfo(unwrapped, cancellationToken).Symbol
                   is IFieldSymbol { IsReadOnly: true } or IPropertySymbol { SetMethod: null };
    }

    private static bool IsSameValue(
        ExpressionSyntax left,
        ExpressionSyntax right,
        SemanticModel model,
        ImmutableArray<INamedTypeSymbol> pathTypes,
        CancellationToken cancellationToken)
    {
        var leftValue = UnwrapGetFullPath(left, model, pathTypes, cancellationToken);
        var rightValue = UnwrapGetFullPath(right, model, pathTypes, cancellationToken);

        var leftSymbol = model.GetSymbolInfo(leftValue, cancellationToken).Symbol;
        var rightSymbol = model.GetSymbolInfo(rightValue, cancellationToken).Symbol;

        var isSameSymbol = leftSymbol is not null
                           && rightSymbol is not null
                           && SymbolEqualityComparer.Default.Equals(leftSymbol, rightSymbol);

        return isSameSymbol
               || string.Equals(Normalize(leftValue), Normalize(rightValue), StringComparison.Ordinal);
    }

    private static ExpressionSyntax UnwrapGetFullPath(
        ExpressionSyntax expression,
        SemanticModel model,
        ImmutableArray<INamedTypeSymbol> pathTypes,
        CancellationToken cancellationToken)
    {
        var unwrapped = ExpressionUnwrapper.ParenthesesAndCasts(expression);

        return FindPathInvocation(unwrapped, GetFullPathMethodName, pathTypes, model, cancellationToken)
                   is { } getFullPath
               && getFullPath.ArgumentList.Arguments.Count > 0
            ? UnwrapGetFullPath(
                getFullPath.ArgumentList.Arguments[0].Expression,
                model,
                pathTypes,
                cancellationToken)
            : unwrapped;
    }

    private static string Normalize(ExpressionSyntax expression)
    {
        return string.Join(
            string.Empty,
            expression.ToString().Where(character => !char.IsWhiteSpace(character)));
    }

    private static string DisplayName(
        ExpressionSyntax path,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var unwrapped = ExpressionUnwrapper.ParenthesesAndCasts(path);

        return model.GetSymbolInfo(unwrapped, cancellationToken).Symbol switch
        {
            IParameterSymbol parameter => parameter.Name,
            IFieldSymbol field => field.Name,
            IPropertySymbol property => property.Name,
            _ => unwrapped.ToString()
        };
    }

    private enum PathSafety
    {
        Safe = 0,
        Unsafe = 1,
        Unknown = 2
    }
}
