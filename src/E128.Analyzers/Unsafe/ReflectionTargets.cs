using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     Resolves the type that a reflection lookup runs against. The receiver is either
///     <c lang="csharp">typeof(T)</c> or <c lang="csharp">value.GetType()</c>. The unsafe
///     reflection rules share this shape.
/// </summary>
internal static class ReflectionTargets
{
    internal static bool TryGet(
        ExpressionSyntax? receiver,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ITypeSymbol? target)
    {
        target = null;

        switch (receiver)
        {
            case TypeOfExpressionSyntax typeOf:
                target = model.GetTypeInfo(typeOf.Type, cancellationToken).Type;
                break;

            case InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "GetType",
                    Expression: var instance,
                },
                ArgumentList.Arguments.Count: 0,
            }:
                target = model.GetTypeInfo(instance, cancellationToken).Type;
                break;

            case IdentifierNameSyntax identifier
                when model.GetSymbolInfo(identifier, cancellationToken).Symbol is ILocalSymbol local:
                return TryGetFromLocal(local, model, cancellationToken, out target);

            default:
                return false;
        }

        return target is not null;
    }

    private static bool TryGetFromLocal(
        ILocalSymbol local,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ITypeSymbol? target)
    {
        target = null;

        var declarator = local.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        return declarator?.Initializer?.Value is { } initializer
               && TryGet(initializer, model, cancellationToken, out target);
    }
}
