using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     Shared symbol and constant helpers for the FIPS cryptography rules. Type matching uses symbols
///     (namespace and name), not source text, so a <c lang="csharp">using</c> alias or <c lang="csharp">using static</c> cannot hide a type.
/// </summary>
internal static class FipsSymbolHelpers
{
    public const string Cryptography = "System.Security.Cryptography";

    private const int MaxResolveDepth = 4;

    private const string HashAlgorithmNameType = "HashAlgorithmName";

    private static readonly SymbolDisplayFormat FullyQualifiedNoGlobal =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

    // Collection and dictionary probes read a hash name to guard against it, not to select it.
    private static readonly ImmutableHashSet<string> HashNameProbeMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Equals", "Contains", "ContainsKey", "IndexOf", "Remove", "TryGetValue");

    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<IFieldSymbol, bool>> AssignedInConstructorCache = new();

    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<ILocalSymbol, bool>> NeverReassignedCache = new();

    /// <summary>
    ///     Returns the fully qualified type name without <c lang="csharp">global::</c>. Call this only when reporting,
    ///     because formatting allocates.
    /// </summary>
    public static string? FullName(ITypeSymbol? type) =>
        type?.ToDisplayString(FullyQualifiedNoGlobal);

    /// <summary>Returns true when the namespace chain equals <paramref name="dotted" />, such as <c lang="csharp">System.Net</c>.</summary>
    public static bool NamespaceEquals(INamespaceSymbol? ns, string dotted)
    {
        var end = dotted.Length;
        for (; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
        {
            if (end < 0)
            {
                return false;
            }

            var dot = end > 0 ? dotted.LastIndexOf('.', end - 1) : -1;
            var start = dot + 1;
            var length = end - start;
            if (ns.Name.Length != length || string.CompareOrdinal(ns.Name, 0, dotted, start, length) != 0)
            {
                return false;
            }

            end = dot;
        }

        return end < 0;
    }

    /// <summary>Returns true when <paramref name="type" /> is a top-level type with the given name and namespace.</summary>
    public static bool IsType(ITypeSymbol? type, string name, string dottedNamespace) =>
        type is INamedTypeSymbol { ContainingType: null } named &&
        string.Equals(named.Name, name, StringComparison.Ordinal) &&
        NamespaceEquals(named.ContainingNamespace, dottedNamespace);

    public static bool IsCryptographyType(ITypeSymbol? type, string name) =>
        IsType(type, name, Cryptography);

    /// <summary>Returns true when <paramref name="type" /> is a top-level cryptography type whose name is in the set.</summary>
    public static bool IsCryptographyType(ITypeSymbol? type, ImmutableHashSet<string> names) =>
        type is INamedTypeSymbol { ContainingType: null } named &&
        names.Contains(named.Name) &&
        NamespaceEquals(named.ContainingNamespace, Cryptography);

    public static bool IsInCryptographyNamespace(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && NamespaceEquals(named.ContainingNamespace, Cryptography);

    /// <summary>
    ///     Returns true when the type sits in System.Security.Cryptography or one of its child namespaces,
    ///     such as <c lang="csharp">System.Security.Cryptography.X509Certificates</c>.
    /// </summary>
    public static bool IsInCryptographyNamespaceTree(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        for (var ns = named.ContainingNamespace; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
        {
            if (NamespaceEquals(ns, Cryptography))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns true when the type, or one of its base types, is the named cryptography type.</summary>
    public static bool DerivesFromCryptographyType(ITypeSymbol? type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsCryptographyType(current, name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns the argument bound to the parameter at <paramref name="ordinal" />. The lookup uses the
    ///     parameter, not the array position, because named arguments can reorder the source.
    /// </summary>
    public static IArgumentOperation? ArgumentAt(ImmutableArray<IArgumentOperation> arguments, int ordinal)
    {
        foreach (var argument in arguments)
        {
            if (argument.Parameter?.Ordinal == ordinal)
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>Returns the argument bound to the parameter with the given name, or null.</summary>
    public static IArgumentOperation? ArgumentNamed(ImmutableArray<IArgumentOperation> arguments, string parameterName)
    {
        foreach (var argument in arguments)
        {
            if (argument.Parameter is { } parameter && string.Equals(parameter.Name, parameterName, StringComparison.Ordinal))
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>
    ///     Returns true when the type sits in the <c lang="csharp">System</c> or <c lang="csharp">Microsoft</c> namespace tree,
    ///     such as <c lang="csharp">System.Collections.Generic.List&lt;T&gt;</c>. A type in any other namespace belongs to
    ///     application or third-party code.
    /// </summary>
    public static bool IsBclType(ITypeSymbol? type)
    {
        var ns = type?.ContainingNamespace;
        if (ns is null || ns.IsGlobalNamespace)
        {
            return false;
        }

        while (ns.ContainingNamespace is { IsGlobalNamespace: false } parent)
        {
            ns = parent;
        }

        return ns.Name is "System" or "Microsoft";
    }

    /// <summary>
    ///     Reports each argument of <paramref name="method" /> that carries a hash name <paramref name="isForbidden" />
    ///     accepts. The arguments are found by parameter type, so named and reordered arguments resolve correctly.
    ///     A probe such as <c lang="csharp">Equals</c> or <c lang="csharp">Contains</c> on a framework type is skipped.
    ///     Each name reports once per call.
    /// </summary>
    public static void ReportHashNameArguments(
        IMethodSymbol? method,
        ImmutableArray<IArgumentOperation> arguments,
        SyntaxNode syntax,
        Func<string, bool> isForbidden,
        Action<SyntaxNode, string> report) =>
        ReportHashNameArguments(
            method,
            arguments,
            syntax,
            (isForbidden, report),
            static (state, name) => state.isForbidden(name),
            static (state, node, name) => state.report(node, name));

    /// <summary>
    ///     Like <see cref="ReportHashNameArguments" /> but passes <paramref name="state" /> to both delegates, so a
    ///     caller can use static lambdas and avoid a closure allocation on every call.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to both delegates.</typeparam>
    public static void ReportHashNameArguments<TState>(
        IMethodSymbol? method,
        ImmutableArray<IArgumentOperation> arguments,
        SyntaxNode syntax,
        TState state,
        Func<TState, string, bool> isForbidden,
        Action<TState, SyntaxNode, string> report)
    {
        var hasHashParameter = false;
        foreach (var argument in arguments)
        {
            if (IsCryptographyType(argument.Parameter?.Type, HashAlgorithmNameType))
            {
                hasHashParameter = true;
                break;
            }
        }

        if (!hasHashParameter)
        {
            return;
        }

        if (method is not null && HashNameProbeMembers.Contains(method.Name) && IsBclType(method.ContainingType))
        {
            return;
        }

        var seen = default(NameSet);
        foreach (var argument in arguments)
        {
            if (IsCryptographyType(argument.Parameter?.Type, HashAlgorithmNameType))
            {
                ReportNames(argument.Value, syntax, ref seen, state, isForbidden, report);
            }
        }
    }

    /// <summary>
    ///     Reports a hash name stored where <see cref="ResolveInitializer" /> cannot follow it: an assignment to any
    ///     <c lang="csharp">HashAlgorithmName</c> target, and the initializer of a mutable field or a property. A <c lang="csharp">readonly</c>
    ///     field with an initializer resolves at its use, so its initializer is skipped.
    /// </summary>
    public static void ReportHashNameStores(
        IOperation operation,
        Func<string, bool> isForbidden,
        Action<SyntaxNode, string> report) =>
        ReportHashNameStores(
            operation,
            (isForbidden, report),
            static (state, name) => state.isForbidden(name),
            static (state, node, name) => state.report(node, name));

    /// <summary>
    ///     Like <see cref="ReportHashNameStores" /> but passes <paramref name="state" /> to both delegates, so a
    ///     caller can use static lambdas and avoid a closure allocation on every operation.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to both delegates.</typeparam>
    public static void ReportHashNameStores<TState>(
        IOperation operation,
        TState state,
        Func<TState, string, bool> isForbidden,
        Action<TState, SyntaxNode, string> report)
    {
        var seen = default(NameSet);
        switch (operation)
        {
            case IFieldInitializerOperation { InitializedFields.Length: 1 } field
                when !field.InitializedFields[0].IsReadOnly && IsCryptographyType(field.InitializedFields[0].Type, HashAlgorithmNameType):
                ReportNames(field.Value, field.Value.Syntax, ref seen, state, isForbidden, report);
                break;

            case IPropertyInitializerOperation { InitializedProperties.Length: 1 } property
                when IsCryptographyType(property.InitializedProperties[0].Type, HashAlgorithmNameType):
                ReportNames(property.Value, property.Value.Syntax, ref seen, state, isForbidden, report);
                break;

            case IFieldInitializerOperation or IPropertyInitializerOperation:
                break;

            default:
                ForEachAssignment(
                    operation,
                    (state, isForbidden, report),
                    static (context, target, value, syntax) =>
                    {
                        if (!IsCryptographyType(target.Type, HashAlgorithmNameType))
                        {
                            return;
                        }

                        var assigned = default(NameSet);
                        ReportNames(value, syntax, ref assigned, context.state, context.isForbidden, context.report);
                    });

                break;
        }
    }

    private static void ReportNames<TState>(
        IOperation value,
        SyntaxNode syntax,
        ref NameSet seen,
        TState state,
        Func<TState, string, bool> isForbidden,
        Action<TState, SyntaxNode, string> report)
    {
        foreach (var name in HashAlgorithmNames(value))
        {
            if (isForbidden(state, name) && seen.Add(name))
            {
                report(state, syntax, name);
            }
        }
    }

    /// <summary>
    ///     Reads every hash name an expression can produce: one name, or both arms of a conditional, a <c lang="csharp">??</c> or
    ///     a switch expression, including arms held in an unreassigned local or a <c lang="csharp">readonly</c> field.
    /// </summary>
    public static IEnumerable<string> HashAlgorithmNames(IOperation? operation, int depth = 0)
    {
        operation = Unwrap(operation);
        if (operation is null || depth > MaxResolveDepth)
        {
            yield break;
        }

        if (operation is IConditionalOperation or ICoalesceOperation or ISwitchExpressionOperation)
        {
            foreach (var name in BranchHashAlgorithmNames(operation, depth))
            {
                yield return name;
            }

            yield break;
        }

        if (TryGetDirectHashAlgorithmName(operation, out var single))
        {
            yield return single;
            yield break;
        }

        if (ResolveInitializer(operation) is { } resolved)
        {
            foreach (var name in HashAlgorithmNames(resolved, depth + 1))
            {
                yield return name;
            }
        }
    }

    /// <summary>Reads the names from both arms of a conditional, a <c lang="csharp">??</c> or a switch expression.</summary>
    private static IEnumerable<string> BranchHashAlgorithmNames(IOperation operation, int depth)
    {
        if (operation is IConditionalOperation conditional)
        {
            foreach (var name in HashAlgorithmNames(conditional.WhenTrue, depth + 1))
            {
                yield return name;
            }

            foreach (var name in HashAlgorithmNames(conditional.WhenFalse, depth + 1))
            {
                yield return name;
            }

            yield break;
        }

        if (operation is ICoalesceOperation coalesce)
        {
            foreach (var name in HashAlgorithmNames(coalesce.Value, depth + 1))
            {
                yield return name;
            }

            foreach (var name in HashAlgorithmNames(coalesce.WhenNull, depth + 1))
            {
                yield return name;
            }

            yield break;
        }

        if (operation is ISwitchExpressionOperation switchExpression)
        {
            foreach (var arm in switchExpression.Arms)
            {
                foreach (var name in HashAlgorithmNames(arm.Value, depth + 1))
                {
                    yield return name;
                }
            }
        }
    }

    /// <summary>Tracks the names already reported for one call site or store, so a repeated name reports once.</summary>
    private struct NameSet
    {
        private string? _first;
        private HashSet<string>? _others;

        public bool Add(string name)
        {
            if (_first is null)
            {
                _first = name;
                return true;
            }

            if (string.Equals(_first, name, StringComparison.Ordinal))
            {
                return false;
            }

            _others ??= new HashSet<string>(StringComparer.Ordinal);

            return _others.Add(name);
        }
    }

    /// <summary>
    ///     Returns the arguments whose parameter type is <c lang="csharp">System.Security.Cryptography.HashAlgorithmName</c>.
    ///     Allocates only when at least one argument matches.
    /// </summary>
    public static ImmutableArray<IArgumentOperation> HashNameArguments(ImmutableArray<IArgumentOperation> arguments)
    {
        var count = 0;
        foreach (var argument in arguments)
        {
            if (IsCryptographyType(argument.Parameter?.Type, HashAlgorithmNameType))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return [];
        }

        var result = ImmutableArray.CreateBuilder<IArgumentOperation>(count);
        foreach (var argument in arguments)
        {
            if (IsCryptographyType(argument.Parameter?.Type, HashAlgorithmNameType))
            {
                result.Add(argument);
            }
        }

        return result.MoveToImmutable();
    }

    /// <summary>
    ///     Returns the first reference to the local that receives <paramref name="creation" /> (through a
    ///     declaration or a plain assignment) in the rest of the same block, or null when the value does not
    ///     land in a local or the local is never used again. <paramref name="block" /> is that block.
    /// </summary>
    public static ILocalReferenceOperation? FindFirstLocalUse(IOperation creation, out IBlockOperation? block)
    {
        block = null;
        var local = GetReceivingLocal(creation);
        if (local is null)
        {
            return null;
        }

        // The block that holds the statement, or the body of a 'using (var x = ...)' statement whose resource
        // is the creation, so the first statement of that body counts as the first use.
        IBlockOperation? parentBlock = null;
        for (var statement = creation; statement.Parent is not null; statement = statement.Parent)
        {
            if (statement.Parent is IUsingOperation { Body: IBlockOperation body } usingStatement && usingStatement.Resources == statement)
            {
                parentBlock = body;
                break;
            }

            if (statement.Parent is IBlockOperation enclosing)
            {
                parentBlock = enclosing;
                break;
            }
        }

        if (parentBlock is null)
        {
            return null;
        }

        block = parentBlock;
        var end = creation.Syntax.Span.End;
        ILocalReferenceOperation? first = null;
        foreach (var descendant in parentBlock.Descendants())
        {
            if (descendant is ILocalReferenceOperation reference &&
                SymbolEqualityComparer.Default.Equals(reference.Local, local) &&
                reference.Syntax.SpanStart >= end &&
                (first is null || reference.Syntax.SpanStart < first.Syntax.SpanStart))
            {
                first = reference;
            }
        }

        return first;
    }

    /// <summary>
    ///     Returns true when <paramref name="use" /> is the receiver of <c lang="csharp">use.Property = value</c> written as its
    ///     own statement directly in <paramref name="block" />, so the assignment runs before any later code.
    /// </summary>
    public static bool IsUnconditionalPropertyAssignment(ILocalReferenceOperation use, string propertyName, IBlockOperation? block) =>
        block is not null &&
        use.Parent is IPropertyReferenceOperation property &&
        string.Equals(property.Property.Name, propertyName, StringComparison.Ordinal) &&
        property.Instance == use &&
        property.Parent is ISimpleAssignmentOperation assignment &&
        assignment.Target == property &&
        assignment.Parent is IExpressionStatementOperation statement &&
        statement.Parent == block;

    /// <summary>
    ///     Returns true when <paramref name="target" /> is a property or field reference named
    ///     <paramref name="memberName" />. Callers use it as a cheap pre-check before any type lookup.
    /// </summary>
    public static bool IsCandidateAssignmentTarget(IOperation? target, string memberName) => target switch
    {
        IPropertyReferenceOperation property => string.Equals(property.Property.Name, memberName, StringComparison.Ordinal),
        IFieldReferenceOperation field => string.Equals(field.Field.Name, memberName, StringComparison.Ordinal),
        _ => false,
    };

    /// <summary>
    ///     Returns false when <paramref name="assignment" /> is a simple or compound assignment whose target cannot be
    ///     the member named <paramref name="memberName" />. A tuple deconstruction returns true, because any element
    ///     can match. Call it before <see cref="ForEachAssignment{TState}" /> to skip an unrelated assignment.
    /// </summary>
    public static bool MayAssignMember(IOperation assignment, string memberName) =>
        assignment is not IAssignmentOperation single ||
        assignment is IDeconstructionAssignmentOperation ||
        IsCandidateAssignmentTarget(single.Target, memberName);

    /// <summary>Returns true when the object initializer of <paramref name="creation" /> assigns the property.</summary>
    public static bool InitializerAssigns(IObjectCreationOperation creation, string propertyName)
    {
        if (creation.Initializer is null)
        {
            return false;
        }

        foreach (var initializer in creation.Initializer.Initializers)
        {
            if (initializer is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation property } &&
                string.Equals(property.Property.Name, propertyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Calls <paramref name="check" /> with each target an assignment writes, the value it receives, and the
    ///     syntax to report. It covers simple, compound, <c lang="csharp">??=</c> and tuple deconstruction assignments.
    /// </summary>
    public static void ForEachAssignment(IOperation operation, Action<IOperation, IOperation, SyntaxNode> check) =>
        ForEachAssignment(operation, check, static (callback, target, value, syntax) => callback(target, value, syntax));

    /// <summary>
    ///     Like <see cref="ForEachAssignment(IOperation, Action{IOperation, IOperation, SyntaxNode})" /> but passes
    ///     <paramref name="state" /> to <paramref name="check" />, so a caller can use a static lambda and avoid a
    ///     closure allocation on every operation.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the check delegate.</typeparam>
    public static void ForEachAssignment<TState>(IOperation operation, TState state, Action<TState, IOperation, IOperation, SyntaxNode> check)
    {
        if (operation is IDeconstructionAssignmentOperation deconstruction)
        {
            if (TryGetDeconstructedPairs(deconstruction, out var targets, out var values))
            {
                for (var i = 0; i < targets.Length; i++)
                {
                    check(state, targets[i], values[i], values[i].Syntax);
                }
            }

            return;
        }

        if (operation is ICompoundAssignmentOperation compound)
        {
            check(state, compound.Target, compound, compound.Syntax);

            return;
        }

        if (operation is IAssignmentOperation assignment)
        {
            check(state, assignment.Target, assignment.Value, assignment.Syntax);
        }
    }

    /// <summary>
    ///     Returns true when <paramref name="operation" />, after any conversions, is an operand of <c lang="csharp">==</c> or
    ///     <c lang="csharp">!=</c>, a constant pattern, a <c lang="csharp">case</c> label, or the argument of <c lang="csharp">Equals</c> or <c lang="csharp">HasFlag</c>.
    ///     A comparison reads a value and does not select it.
    /// </summary>
    public static bool IsComparison(IOperation operation)
    {
        var parent = operation.Parent;
        while (parent is IConversionOperation)
        {
            parent = parent.Parent;
        }

        return parent is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals }
            or IConstantPatternOperation
            or ISingleValueCaseClauseOperation
            or IArgumentOperation { Parent: IInvocationOperation { TargetMethod.Name: "Equals" or "HasFlag" } };
    }

    /// <summary>Removes conversion wrappers to reach the value an expression produces.</summary>
    public static IOperation? Unwrap(IOperation? operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    /// <summary>
    ///     Reads a compile-time constant of type <typeparamref name="T" /> (<see cref="int" /> or
    ///     <see cref="string" />), including <c lang="csharp">const</c> fields, folded expressions and string concatenations.
    /// </summary>
    /// <typeparam name="T">The constant type.</typeparam>
    public static bool TryGetConstant<T>(IOperation? operation, [MaybeNullWhen(false)] out T value)
    {
        if (operation is { ConstantValue: { HasValue: true, Value: T constant } })
        {
            value = constant;
            return true;
        }

        value = default;

        return false;
    }

    /// <summary>
    ///     Reads a compile-time constant held in an unreassigned local or a readonly field that no constructor
    ///     writes. A parameter or a reassigned value is not resolved.
    /// </summary>
    /// <typeparam name="T">The constant type.</typeparam>
    public static bool TryGetResolvedConstant<T>(IOperation? operation, [MaybeNullWhen(false)] out T value, int depth = 0)
    {
        operation = Unwrap(operation);

        return TryGetConstant(operation, out value) ||
            (depth < MaxResolveDepth &&
             ResolveInitializer(operation) is { } resolved &&
             TryGetResolvedConstant(resolved, out value, depth + 1));
    }

    /// <summary>
    ///     Returns the initializer of the field or local that <paramref name="operation" /> reads, or null. Only a
    ///     <c lang="csharp">readonly</c> field that no constructor of its type assigns, or a local that nothing assigns after its
    ///     declaration, resolves, so the initializer is the value every read sees. A declaration in a syntax tree
    ///     outside the compilation (a field from a referenced project) does not resolve.
    /// </summary>
    public static IOperation? ResolveInitializer(IOperation? operation)
    {
        operation = Unwrap(operation);

        if (operation?.SemanticModel is not { } model)
        {
            return null;
        }

        SyntaxReference? declaration = null;
        if (operation is IFieldReferenceOperation { Field.IsReadOnly: true } field)
        {
            if (field.Field.DeclaringSyntaxReferences.Length == 1 &&
                model.Compilation.ContainsSyntaxTree(field.Field.DeclaringSyntaxReferences[0].SyntaxTree) &&
                !IsAssignedInConstructor(field.Field, model.Compilation))
            {
                declaration = field.Field.DeclaringSyntaxReferences[0];
            }
        }
        else if (operation is ILocalReferenceOperation local &&
                 IsNeverReassigned(local) &&
                 local.Local.DeclaringSyntaxReferences.Length == 1)
        {
            declaration = local.Local.DeclaringSyntaxReferences[0];
        }

        var syntax = declaration?.GetSyntax();

        return syntax is VariableDeclaratorSyntax { Initializer.Value: { } value } &&
            model.Compilation.ContainsSyntaxTree(syntax.SyntaxTree)
                ? model.Compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(value)
                : null;
    }

    /// <summary>
    ///     Reads the algorithm name from a <c lang="csharp">HashAlgorithmName.X</c> property or a
    ///     <c lang="csharp">new HashAlgorithmName("X")</c> expression with a constant name, a <c lang="csharp">HashAlgorithmName.FromOid</c> call
    ///     with a known constant object identifier, either written inline or held in a <c lang="csharp">readonly</c> field or an
    ///     unreassigned local.
    /// </summary>
    public static bool TryGetHashAlgorithmName(IOperation? operation, out string name, int depth = 0)
    {
        name = string.Empty;
        operation = Unwrap(operation);

        return TryGetDirectHashAlgorithmName(operation, out name) ||
            (depth < MaxResolveDepth &&
             ResolveInitializer(operation) is { } resolved &&
             TryGetHashAlgorithmName(resolved, out name, depth + 1));
    }

    private static bool TryGetDirectHashAlgorithmName(IOperation? operation, out string name)
    {
        name = string.Empty;
        operation = Unwrap(operation);

        if (operation is IPropertyReferenceOperation { Property: { IsStatic: true } property } &&
            IsCryptographyType(property.ContainingType, HashAlgorithmNameType))
        {
            name = property.Name;
            return true;
        }

        if (operation is IObjectCreationOperation creation &&
            IsCryptographyType(creation.Type, HashAlgorithmNameType) &&
            creation.Arguments.Length == 1 &&
            TryGetConstant<string>(creation.Arguments[0].Value, out var text))
        {
            name = text;
            return true;
        }

        if (operation is IInvocationOperation { TargetMethod: { IsStatic: true, Name: "FromOid" } fromOid } oidCall &&
            IsCryptographyType(fromOid.ContainingType, HashAlgorithmNameType) &&
            TryGetConstant<string>(ArgumentAt(oidCall.Arguments, 0)?.Value, out var oid) &&
            HashNameFromOid(oid) is { } oidName)
        {
            name = oidName;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Reads the byte length of a buffer when it is a fixed-size array (<c lang="csharp">new byte[N]</c>,
    ///     <c lang="csharp">new byte[] { ... }</c>), a collection expression, a <c lang="csharp">stackalloc</c>, or an encoded
    ///     string with a constant argument, either written inline or held in a <c lang="csharp">readonly</c> field or an
    ///     unreassigned local.
    /// </summary>
    public static bool TryGetConstantByteLength(IOperation? operation, out int length, int depth = 0)
    {
        length = 0;
        operation = Unwrap(operation);

        if (operation is IInvocationOperation invocation && TryGetKnownByteLength(invocation, out length, depth))
        {
            return true;
        }

        switch (operation)
        {
            case null:
                return false;

            case IArrayCreationOperation { DimensionSizes.Length: 1 } array:
                if (TryGetConstant<int>(array.DimensionSizes[0], out var size))
                {
                    length = size;
                    return true;
                }

                if (array.Initializer is not null)
                {
                    length = array.Initializer.ElementValues.Length;
                    return true;
                }

                return false;

            case ICollectionExpressionOperation collection:
                foreach (var element in collection.Elements)
                {
                    if (element is ISpreadOperation)
                    {
                        return false;
                    }
                }

                length = collection.Elements.Length;
                return true;

            case IImplicitIndexerReferenceOperation { Argument: { } rangeArgument } indexer
                when Unwrap(rangeArgument) is IRangeOperation range:
                return TryGetRangeLength(indexer.Instance, range.LeftOperand, range.RightOperand, depth, out length);

            case IArrayElementReferenceOperation { Indices.Length: 1 } element
                when Unwrap(element.Indices[0]) is IRangeOperation arrayRange:
                return TryGetRangeLength(element.ArrayReference, arrayRange.LeftOperand, arrayRange.RightOperand, depth, out length);

            case IFieldReferenceOperation or ILocalReferenceOperation:
                return depth < MaxResolveDepth &&
                    ResolveInitializer(operation) is { } resolved &&
                    TryGetConstantByteLength(resolved, out length, depth + 1);

            default:
                // Roslyn does not expose a public operation interface for stackalloc, so read the syntax.
                return TryGetStackAllocLength(operation, out length);
        }
    }

    private static bool TryGetKnownByteLength(IInvocationOperation invocation, out int length, int depth)
    {
        length = 0;
        if (depth >= MaxResolveDepth)
        {
            return false;
        }

        if (TryGetSliceLength(invocation, out length, depth))
        {
            return true;
        }

        if (string.Equals(invocation.TargetMethod.Name, "GetBytes", StringComparison.Ordinal) &&
            NamespaceEquals(invocation.TargetMethod.ContainingNamespace, "System.Text") &&
            Unwrap(ResolveInitializer(invocation.Instance) ?? invocation.Instance) is IPropertyReferenceOperation encodingProperty &&
            encodingProperty.Property.IsStatic &&
            IsType(encodingProperty.Property.ContainingType, "Encoding", "System.Text") &&
            TryGetResolvedConstant<string>(ArgumentAt(invocation.Arguments, 0)?.Value, out var text, depth + 1) &&
            GetEncoding(encodingProperty.Property.Name) is { } encoding)
        {
            length = encoding.GetByteCount(text);
            return true;
        }

        return invocation.TargetMethod.IsStatic &&
            IsType(invocation.TargetMethod.ContainingType, "Convert", "System") &&
            invocation.Arguments.Length == 1 &&
            TryGetResolvedConstant<string>(invocation.Arguments[0].Value, out var encoded, depth + 1) &&
            invocation.TargetMethod.Name switch
            {
                "FromBase64String" => TryGetBase64ByteLength(encoded, out length),
                "FromHexString" => TryGetHexByteLength(encoded, out length),
                _ => false,
            };
    }

    /// <summary>
    ///     Reads the length of a slice: <c lang="csharp">AsSpan</c> or <c lang="csharp">AsMemory</c> with a start, a
    ///     length or a range, an instance <c lang="csharp">Slice</c>, or
    ///     <c lang="csharp">RuntimeHelpers.GetSubArray</c>. A constant length argument decides the result even when
    ///     the source length is unknown.
    /// </summary>
    private static bool TryGetSliceLength(IInvocationOperation invocation, out int length, int depth)
    {
        length = 0;
        var method = invocation.TargetMethod;
        var isSubArray = string.Equals(method.Name, "GetSubArray", StringComparison.Ordinal) &&
            IsType(method.ContainingType, "RuntimeHelpers", "System.Runtime.CompilerServices");

        if (!isSubArray &&
            ((!string.Equals(method.Name, "Slice", StringComparison.Ordinal) &&
              !string.Equals(method.Name, "AsSpan", StringComparison.Ordinal) &&
              !string.Equals(method.Name, "AsMemory", StringComparison.Ordinal)) ||
             !NamespaceEquals(method.ContainingNamespace, "System")))
        {
            return false;
        }

        // An extension method receives the source as its first argument, so the extra arguments follow it.
        var source = invocation.Instance ?? ArgumentAt(invocation.Arguments, 0)?.Value;
        var extraArguments = invocation.Instance is null ? invocation.Arguments.Length - 1 : invocation.Arguments.Length;

        if (ArgumentNamed(invocation.Arguments, "range") is { } rangeArgument)
        {
            return Unwrap(rangeArgument.Value) is IRangeOperation range &&
                TryGetRangeLength(source, range.LeftOperand, range.RightOperand, depth, out length);
        }

        if (ArgumentNamed(invocation.Arguments, nameof(length)) is { } lengthArgument)
        {
            if (TryGetResolvedConstant<int>(lengthArgument.Value, out var count, depth + 1) && count >= 0)
            {
                length = count;

                return true;
            }

            return false;
        }

        if (extraArguments == 0)
        {
            return !isSubArray && TryGetConstantByteLength(source, out length, depth + 1);
        }

        var startArgument = ArgumentNamed(invocation.Arguments, "start") ?? ArgumentNamed(invocation.Arguments, "startIndex");
        if (startArgument is null ||
            !TryGetIndexValue(startArgument.Value, source, depth, out var start) ||
            !TryGetConstantByteLength(source, out var total, depth + 1) ||
            total < start)
        {
            return false;
        }

        length = total - start;

        return true;
    }

    /// <summary>
    ///     Reads the length of <c lang="csharp">source[left..right]</c>. An omitted left bound is 0. An omitted right
    ///     bound, or an index from the end (<c lang="csharp">^4</c>), needs a source of known length.
    /// </summary>
    private static bool TryGetRangeLength(IOperation? source, IOperation? left, IOperation? right, int depth, out int length)
    {
        length = 0;
        if (depth >= MaxResolveDepth)
        {
            return false;
        }

        var start = 0;
        if (left is not null && !TryGetIndexValue(left, source, depth, out start))
        {
            return false;
        }

        int end;
        if (right is null)
        {
            if (!TryGetConstantByteLength(source, out end, depth + 1))
            {
                return false;
            }
        }
        else if (!TryGetIndexValue(right, source, depth, out end))
        {
            return false;
        }

        if (end < start)
        {
            return false;
        }

        length = end - start;

        return true;
    }

    /// <summary>
    ///     Reads a constant index, or an index from the end (<c lang="csharp">^4</c>) when the length of
    ///     <paramref name="source" /> is known.
    /// </summary>
    private static bool TryGetIndexValue(IOperation index, IOperation? source, int depth, out int value)
    {
        value = 0;
        var operation = Unwrap(index);
        if (operation is IUnaryOperation { OperatorKind: UnaryOperatorKind.Hat } hat)
        {
            if (!TryGetResolvedConstant<int>(hat.Operand, out var fromEnd, depth + 1) ||
                !TryGetConstantByteLength(source, out var total, depth + 1) ||
                total < fromEnd)
            {
                return false;
            }

            value = total - fromEnd;

            return true;
        }

        return TryGetResolvedConstant(operation, out value, depth + 1) && value >= 0;
    }

    private static bool TryGetBase64ByteLength(string value, out int length)
    {
        try
        {
            length = Convert.FromBase64String(value).Length;
            return true;
        }
        catch (FormatException)
        {
            length = 0;
            return false;
        }
    }

    private static bool TryGetHexByteLength(string value, out int length)
    {
        length = 0;
        if (value.Length % 2 != 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        length = value.Length >> 1;
        return true;
    }

    private static Encoding? GetEncoding(string propertyName) => propertyName switch
    {
        "UTF8" => Encoding.UTF8,
        "ASCII" => Encoding.ASCII,
        "Unicode" => Encoding.Unicode,
        "Latin1" => Encoding.GetEncoding("iso-8859-1"),
        "UTF32" => Encoding.UTF32,
        _ => null,
    };

    /// <summary>
    ///     Returns true when <paramref name="word" /> appears in <paramref name="name" /> as a whole word:
    ///     bounded by the start or end, an underscore, a digit, or a camelCase change.
    /// </summary>
    public static bool ContainsWord(string name, string word)
    {
        var camelCase = false;
        foreach (var character in name)
        {
            if (char.IsLower(character))
            {
                camelCase = true;
                break;
            }
        }

        var index = 0;
        while ((index = name.IndexOf(word, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var afterPosition = index + word.Length;
            var before = index == 0 ? '\0' : name[index - 1];
            var after = afterPosition >= name.Length ? '\0' : name[afterPosition];

            var boundaryBefore = before == '\0' || before == '_' || char.IsDigit(before) || (camelCase && char.IsUpper(name[index]));
            var boundaryAfter = after == '\0' || after == '_' || char.IsDigit(after) || (camelCase && char.IsUpper(after));

            if (boundaryBefore && boundaryAfter)
            {
                return true;
            }

            index = afterPosition;
        }

        return false;
    }

    private static ILocalSymbol? GetReceivingLocal(IOperation creation)
    {
        var child = creation;
        var current = creation.Parent;
        while (current is IConversionOperation)
        {
            child = current;
            current = current.Parent;
        }

        switch (current)
        {
            case IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator }:
                return declarator.Symbol;
            case ISimpleAssignmentOperation { Target: ILocalReferenceOperation target } assignment when assignment.Value == child:
                return target.Local;
            default:
                return null;
        }
    }

    private static bool TryGetDeconstructedPairs(
        IDeconstructionAssignmentOperation assignment,
        out ImmutableArray<IOperation> targets,
        out ImmutableArray<IOperation> values)
    {
        if (assignment.Target is ITupleOperation target &&
            Unwrap(assignment.Value) is ITupleOperation value &&
            target.Elements.Length == value.Elements.Length)
        {
            targets = target.Elements;
            values = value.Elements;

            return true;
        }

        targets = values = default;

        return false;
    }

    /// <summary>
    ///     Returns true when a constructor or static constructor of the field's type writes the field. A
    ///     <c lang="csharp">readonly</c> field then holds its initializer only until the constructor runs.
    /// </summary>
    private static bool IsAssignedInConstructor(IFieldSymbol field, Compilation compilation)
    {
        var cache = AssignedInConstructorCache.GetValue(
            compilation,
            static _ => new ConcurrentDictionary<IFieldSymbol, bool>(SymbolEqualityComparer.Default));

        if (cache.TryGetValue(field, out var assigned))
        {
            return assigned;
        }

        assigned = ComputeIsAssignedInConstructor(field, compilation);
        cache.TryAdd(field, assigned);

        return assigned;
    }

    private static bool ComputeIsAssignedInConstructor(IFieldSymbol field, Compilation compilation)
    {
        foreach (var constructor in field.ContainingType.Constructors)
        {
            if (HasFieldWrite(constructor, field, compilation))
            {
                return true;
            }
        }

        foreach (var member in field.ContainingType.GetMembers())
        {
            if (member is IPropertySymbol { SetMethod.IsInitOnly: true, SetMethod: { } setter } &&
                HasFieldWrite(setter, field, compilation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFieldWrite(IMethodSymbol method, IFieldSymbol field, Compilation compilation)
    {
        foreach (var reference in method.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax();
            if (!compilation.ContainsSyntaxTree(syntax.SyntaxTree) ||
                compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax) is not { } body)
            {
                continue;
            }

            foreach (var descendant in body.Descendants())
            {
                if (descendant is IFieldReferenceOperation other &&
                    SymbolEqualityComparer.Default.Equals(other.Field, field) &&
                    IsWrite(other))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns true when no statement in the enclosing body assigns the local, passes it by <c lang="csharp">ref</c> or
    ///     <c lang="csharp">out</c>, or increments it.
    /// </summary>
    private static bool IsNeverReassigned(ILocalReferenceOperation reference)
    {
        if (reference.SemanticModel is not { } model)
        {
            return ComputeIsNeverReassigned(reference);
        }

        var cache = NeverReassignedCache.GetValue(
            model.Compilation,
            static _ => new ConcurrentDictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default));

        if (cache.TryGetValue(reference.Local, out var neverReassigned))
        {
            return neverReassigned;
        }

        neverReassigned = ComputeIsNeverReassigned(reference);
        cache.TryAdd(reference.Local, neverReassigned);

        return neverReassigned;
    }

    private static bool ComputeIsNeverReassigned(ILocalReferenceOperation reference)
    {
        var root = (IOperation)reference;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        foreach (var descendant in root.Descendants())
        {
            if (descendant is ILocalReferenceOperation other &&
                SymbolEqualityComparer.Default.Equals(other.Local, reference.Local) &&
                IsWrite(other))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWrite(IOperation reference) => reference.Parent switch
    {
        IAssignmentOperation assignment => assignment.Target == reference,
        IIncrementOrDecrementOperation => true,
        IArgumentOperation argument => argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out,
        ITupleOperation { Parent: IDeconstructionAssignmentOperation deconstruction } tuple => deconstruction.Target == tuple,
        _ => false,
    };

    private static string? HashNameFromOid(string oid) => oid.Trim() switch
    {
        "1.3.14.3.2.26" => "SHA1",
        "1.2.840.113549.2.5" => "MD5",
        "1.3.36.3.2.1" => "RIPEMD160",
        "2.16.840.1.101.3.4.2.8" => "SHA3_256",
        "2.16.840.1.101.3.4.2.9" => "SHA3_384",
        "2.16.840.1.101.3.4.2.10" => "SHA3_512",
        _ => null,
    };

    private static bool TryGetStackAllocLength(IOperation operation, out int length)
    {
        length = 0;
        if (operation.Syntax is ImplicitStackAllocArrayCreationExpressionSyntax implicitStackAlloc)
        {
            length = implicitStackAlloc.Initializer.Expressions.Count;
            return true;
        }

        if (operation.Syntax is not StackAllocArrayCreationExpressionSyntax stackAlloc ||
            stackAlloc.Type is not ArrayTypeSyntax { RankSpecifiers.Count: 1 } arrayType ||
            arrayType.RankSpecifiers[0].Sizes.Count != 1)
        {
            return false;
        }

        var sizeExpression = arrayType.RankSpecifiers[0].Sizes[0];
        if (sizeExpression is OmittedArraySizeExpressionSyntax)
        {
            if (stackAlloc.Initializer is null)
            {
                return false;
            }

            length = stackAlloc.Initializer.Expressions.Count;
            return true;
        }

        var model = operation.SemanticModel;
        if (model is null)
        {
            return false;
        }

        var constant = model.GetConstantValue(sizeExpression);
        if (constant.HasValue && constant.Value is int count)
        {
            length = count;
            return true;
        }

        return false;
    }
}
