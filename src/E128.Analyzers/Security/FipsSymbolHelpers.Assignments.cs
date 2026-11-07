using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

internal static partial class FipsSymbolHelpers
{
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
        var root = (IOperation)reference;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        // One walk per body collects every written local, so each later reference is a set lookup.
        var written = WrittenLocalsCache.GetValue(root, ComputeWrittenLocals);

        return !written.Contains(reference.Local);
    }

    private static HashSet<ILocalSymbol> ComputeWrittenLocals(IOperation root)
    {
        var written = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);

        foreach (var descendant in root.Descendants())
        {
            if (descendant is ILocalReferenceOperation other && IsWrite(other))
            {
                written.Add(other.Local);
            }
        }

        return written;
    }

    private static bool IsWrite(IOperation reference) => reference.Parent switch
    {
        IAssignmentOperation assignment => assignment.Target == reference,
        IIncrementOrDecrementOperation => true,
        IArgumentOperation argument => argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out,
        ITupleOperation { Parent: IDeconstructionAssignmentOperation deconstruction } tuple => deconstruction.Target == tuple,
        _ => false,
    };
}
