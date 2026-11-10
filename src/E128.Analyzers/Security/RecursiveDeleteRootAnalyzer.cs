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
///     E128184: Reports a recursive directory delete whose target path the rule does not recognize
///     as safe. A path built from a parameter, a field, or a property can point outside the intended
///     root.
/// </summary>
/// <remarks>
///     The rule reports a path it cannot trace to a recognized safe root, and it never follows a
///     path across a method boundary. A path rooted at a temp directory, a relative literal, a
///     <c lang="csharp">Path.GetFullPath</c> result of either, or a value guarded by a
///     <c lang="csharp">StartsWith</c> check against a directory-bounded root stays silent.
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
        "Recursive delete outside a recognized safe root",
        "'{0}' is deleted recursively, and the rule does not recognize the path as a temp directory or a fixed root",
        "E128-Security",
        DiagnosticSeverity.Info,
        true,
        "A recursive delete of a path the rule does not recognize as safe can remove files outside the intended root. Build the path from Path.GetTempPath(), use a relative string literal, or guard it with StartsWith against a fixed root.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
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

        if (ClassifyPath(path, model, pathTypes, cancellationToken) is not PathSafety.Unsafe)
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

        // Syntactic gate first. Most invocations are not a delete, so they must not pay for
        // overload resolution.
        if (!MentionsDeleteName(invocation))
        {
            return false;
        }

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
                || method.Parameters[1].Type.SpecialType is not SpecialType.System_Boolean)
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

        return MetadataTypes.Contains(directoryInfoTypes, method.ContainingType)
               && TryGetDirectoryInfoArguments(invocation, method, out path, out recursive);
    }

    // DirectoryInfo.Delete(bool). The instance owns the path.
    private static bool TryGetDirectoryInfoArguments(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        out ExpressionSyntax path,
        out ExpressionSyntax recursive)
    {
        path = invocation;
        recursive = invocation;

        if (method.Parameters.Length is not 1
            || method.Parameters[0].Type.SpecialType is not SpecialType.System_Boolean
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || FindArgument(invocation, method, parameterIndex: 0) is not { } recursiveArgument)
        {
            return false;
        }

        path = memberAccess.Expression;
        recursive = recursiveArgument;
        return true;
    }

    private static bool MentionsDeleteName(InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };

        return string.Equals(name, "Delete", StringComparison.Ordinal);
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
    ///     Decides whether the path stays inside a temp directory or a fixed root. A local alias is
    ///     followed first, so <c lang="csharp">var target = path</c> classifies as <c lang="csharp">path</c>.
    /// </summary>
    private static PathSafety ClassifyPath(
        ExpressionSyntax expression,
        SemanticModel model,
        ImmutableArray<INamedTypeSymbol> pathTypes,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var current = expression;

        while (true)
        {
            var unwrapped = ExpressionUnwrapper.ParenthesesAndCasts(current);
            var symbol = model.GetSymbolInfo(unwrapped, cancellationToken).Symbol;

            if (symbol is not ILocalSymbol local || !visited.Add(local))
            {
                break;
            }

            if (GetLocalInitializer(local, cancellationToken) is not { } initializer)
            {
                break;
            }

            current = initializer;
        }

        return ClassifyPathCore(current, model, pathTypes, cancellationToken);
    }

    private static PathSafety ClassifyPathCore(
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

        // A rooted literal names an absolute location the rule cannot verify. Only a relative
        // literal counts as a fixed root.
        if (IsRootedLiteral(unwrapped, model, cancellationToken))
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
            return ClassifyCombine(combine, model, pathTypes, cancellationToken);
        }

        return IsParameterOrMemberRead(unwrapped, model, cancellationToken)
            ? PathSafety.Unsafe
            : PathSafety.Unknown;
    }

    // Path.Combine(first, ...) takes its safety from the first argument.
    private static PathSafety ClassifyCombine(
        InvocationExpressionSyntax combine,
        SemanticModel model,
        ImmutableArray<INamedTypeSymbol> pathTypes,
        CancellationToken cancellationToken)
    {
        var arguments = combine.ArgumentList.Arguments;
        if (arguments.Count is 0)
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

        return string.Equals(trimmed, "/", StringComparison.Ordinal)
               || string.Equals(trimmed, "\\", StringComparison.Ordinal)
               || IsDriveRoot(trimmed);
    }

    // "C:" or "C:\\" or "C:/".
    private static bool IsDriveRoot(string value)
    {
        const int bareDriveLength = 2;
        const int separatedDriveLength = 3;

        if (value.Length is not (bareDriveLength or separatedDriveLength)
            || !char.IsLetter(value[0])
            || value[1] is not ':')
        {
            return false;
        }

        return value.Length is bareDriveLength || value[bareDriveLength] is '\\' or '/';
    }

    // A constant string that starts at a filesystem root: a leading separator, a UNC prefix, or a
    // drive letter plus a colon.
    private static bool IsRootedLiteral(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetConstantValue(expression, cancellationToken).Value is string value
               && IsRootedPath(value);
    }

    private static bool IsRootedPath(string value)
    {
        return value.Length is not 0
               && (value[0] is '/' or '\\'
                   || (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':'));
    }

    private static bool EndsWithDirectorySeparator(string value)
    {
        return value.Length > 0 && value[value.Length - 1] is '/' or '\\';
    }

    private static ExpressionSyntax? GetLocalInitializer(ILocalSymbol local, CancellationToken cancellationToken)
    {
        foreach (var reference in local.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is VariableDeclaratorSyntax declarator
                && declarator.Initializer is { } initializer)
            {
                return initializer.Value;
            }
        }

        return null;
    }

    private static ExpressionSyntax? GetLocalInitializer(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var unwrapped = ExpressionUnwrapper.ParenthesesAndCasts(expression);

        return model.GetSymbolInfo(unwrapped, cancellationToken).Symbol is ILocalSymbol local
            ? GetLocalInitializer(local, cancellationToken)
            : null;
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
                   && field.Type.SpecialType is SpecialType.System_String);
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
    ///     Returns true when the statement directly before the delete, or an if that encloses it,
    ///     tests the deleted path with <c lang="csharp">StartsWith</c> against a directory-bounded root
    ///     using an ordinal comparison.
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

                // A culture-sensitive comparison and a bare prefix both prove too little to treat the
                // path as contained.
                var rootArgument = FindArgumentExpression(invocation, parameterIndex: 0, "value");
                if (!IsOrdinalComparison(invocation, model, cancellationToken)
                    || !IsSeparatorBoundedRoot(rootArgument, model, pathTypes, cancellationToken)
                    || !IsSafeRoot(rootArgument, model, pathTypes, cancellationToken))
                {
                    continue;
                }

                // A write to the checked value between the guard and the delete makes the guard stale.
                if (IsSameValue(subject, path, model, pathTypes, cancellationToken)
                    && !IsAssignedBetween(ifStatement, encloses, subject, deleteInvocation, model, cancellationToken))
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

            if (parent is null || !CanPassThrough(parent, allowedBinary, encloses, ref seenNot))
            {
                return false;
            }

            node = parent;
        }

        return encloses || seenNot;
    }

    private static bool CanPassThrough(
        SyntaxNode parent,
        SyntaxKind allowedBinary,
        bool encloses,
        ref bool seenNot)
    {
        // Parentheses never change the value.
        if (parent is ParenthesizedExpressionSyntax)
        {
            return true;
        }

        if (parent is PrefixUnaryExpressionSyntax prefix
            && prefix.IsKind(SyntaxKind.LogicalNotExpression)
            && !encloses
            && !seenNot)
        {
            seenNot = true;

            return true;
        }

        // The other operand of a binary operator does not change what the call proves.
        return parent is BinaryExpressionSyntax binary
               && binary.IsKind(allowedBinary)
               && (encloses || seenNot);
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

            if (current is BlockSyntax block && FindPrecedingIf(block, deleteInvocation) is { } preceding)
            {
                yield return (preceding, false);
            }

            if (current is MemberDeclarationSyntax)
            {
                break;
            }
        }
    }

    // Only the statement directly before the delete can guard it. An earlier if is too far away to
    // prove the value at the delete.
    private static IfStatementSyntax? FindPrecedingIf(BlockSyntax block, SyntaxNode deleteInvocation)
    {
        for (var index = 1; index < block.Statements.Count; index++)
        {
            if (block.Statements[index].Span.Contains(deleteInvocation.Span)
                && block.Statements[index - 1] is IfStatementSyntax preceding)
            {
                return preceding;
            }
        }

        return null;
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

    private static ExpressionSyntax? FindArgumentExpression(
        InvocationExpressionSyntax invocation,
        int parameterIndex,
        string parameterName)
    {
        var positionalIndex = 0;

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.NameColon is { } nameColon)
            {
                if (string.Equals(
                        nameColon.Name.Identifier.ValueText,
                        parameterName,
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
    ///     True when the call passes <c lang="csharp">StringComparison.Ordinal</c> or
    ///     <c lang="csharp">StringComparison.OrdinalIgnoreCase</c>. The single-argument overload and a
    ///     culture-sensitive comparison compare with the current culture, so neither proves a path
    ///     boundary.
    /// </summary>
    private static bool IsOrdinalComparison(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var comparison = FindArgumentExpression(invocation, parameterIndex: 1, "comparisonType");

        return comparison is not null
               && model.GetSymbolInfo(comparison, cancellationToken).Symbol is IFieldSymbol field
               && string.Equals(field.ContainingType.ToDisplayString(), "System.StringComparison", StringComparison.Ordinal)
               && (string.Equals(field.Name, "Ordinal", StringComparison.Ordinal)
                   || string.Equals(field.Name, "OrdinalIgnoreCase", StringComparison.Ordinal));
    }

    /// <summary>
    ///     True when the root ends at a directory boundary. A bare prefix such as /srv/app/data also
    ///     matches /srv/app/data-backup, so the root must end in a separator or be built with one.
    /// </summary>
    private static bool IsSeparatorBoundedRoot(
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

        // Path.GetTempPath returns a path that ends with a directory separator.
        return FindPathInvocation(unwrapped, GetTempPathMethodName, pathTypes, model, cancellationToken) is not null
               || (model.GetConstantValue(unwrapped, cancellationToken).Value is string constant
                   && EndsWithDirectorySeparator(constant))
               || (unwrapped is BinaryExpressionSyntax binary
                   && binary.IsKind(SyntaxKind.AddExpression)
                   && IsSeparatorExpression(binary.Right, pathTypes, model, cancellationToken))
               || (GetLocalInitializer(unwrapped, model, cancellationToken) is { } initializer
                   && IsSeparatorBoundedRoot(initializer, model, pathTypes, cancellationToken));
    }

    private static bool IsSeparatorExpression(
        ExpressionSyntax expression,
        ImmutableArray<INamedTypeSymbol> pathTypes,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var unwrapped = ExpressionUnwrapper.ParenthesesAndCasts(expression);

        if (model.GetConstantValue(unwrapped, cancellationToken).Value is string constant)
        {
            return EndsWithDirectorySeparator(constant);
        }

        if (unwrapped is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var name = memberAccess.Name.Identifier.ValueText;

        return (string.Equals(name, "DirectorySeparatorChar", StringComparison.Ordinal)
                || string.Equals(name, "AltDirectorySeparatorChar", StringComparison.Ordinal))
               && model.GetSymbolInfo(unwrapped, cancellationToken).Symbol is IFieldSymbol { ContainingType: { } type }
               && MetadataTypes.Contains(pathTypes, type);
    }

    /// <summary>
    ///     True when the guarded value is written between the guard and the delete. The write makes
    ///     the guard stale, so it no longer proves anything about the path.
    /// </summary>
    private static bool IsAssignedBetween(
        IfStatementSyntax guard,
        bool encloses,
        ExpressionSyntax subject,
        InvocationExpressionSyntax deleteInvocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var subjectSymbol = model.GetSymbolInfo(
            ExpressionUnwrapper.ParenthesesAndCasts(subject),
            cancellationToken).Symbol;

        if (subjectSymbol is null)
        {
            return false;
        }

        // An enclosing guard proves the path from the end of its condition. A preceding guard proves
        // it from the end of its statement.
        var guardEnd = encloses ? guard.Condition.Span.End : guard.Span.End;
        var deleteStart = deleteInvocation.SpanStart;
        var scope = guard.Parent ?? guard;

        foreach (var node in scope.DescendantNodes())
        {
            if (node.SpanStart >= guardEnd
                && node.SpanStart < deleteStart
                && IsWriteTo(node, subjectSymbol, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWriteTo(
        SyntaxNode node,
        ISymbol subject,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var target = node switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left,
            PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.PreIncrementExpression)
                                                    || prefix.IsKind(SyntaxKind.PreDecrementExpression) => prefix.Operand,
            PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.PostIncrementExpression)
                                                      || postfix.IsKind(SyntaxKind.PostDecrementExpression) => postfix.Operand,
            _ => null
        };

        return target is not null
               && SymbolEqualityComparer.Default.Equals(
                   model.GetSymbolInfo(ExpressionUnwrapper.ParenthesesAndCasts(target), cancellationToken).Symbol,
                   subject);
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

        // A run-time-computed value is not a recognized root, so a readonly field and a get-only
        // property do not qualify. A local does, when its single initializer is a safe root.
        if (IsFixedRoot(unwrapped, model, cancellationToken)
            || IsTempRooted(unwrapped, model, pathTypes, cancellationToken))
        {
            return true;
        }

        if (getFullPath is { } invocation && invocation.ArgumentList.Arguments.Count > 0)
        {
            return IsSafeRoot(
                invocation.ArgumentList.Arguments[0].Expression,
                model,
                pathTypes,
                cancellationToken);
        }

        // root + separator still starts at the root.
        return (unwrapped is BinaryExpressionSyntax binary
                && binary.IsKind(SyntaxKind.AddExpression)
                && IsSafeRoot(binary.Left, model, pathTypes, cancellationToken))
               || (GetLocalInitializer(unwrapped, model, cancellationToken) is { } initializer
                   && IsSafeRoot(initializer, model, pathTypes, cancellationToken));
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
            expression.ToString().Where(static character => !char.IsWhiteSpace(character)));
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
