using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class LocalTargets
{
    /// <summary>
    ///     Returns the local that receives <paramref name="creation" />, through a declaration
    ///     initializer or a simple assignment to an identifier. Returns null for every other shape.
    /// </summary>
    internal static ILocalSymbol? FromCreation(
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return creation.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } =>
                semanticModel.GetDeclaredSymbol(declarator, cancellationToken) as ILocalSymbol,
            AssignmentExpressionSyntax assignment when assignment.Right == creation
                                                       && assignment.Left is IdentifierNameSyntax =>
                semanticModel.GetSymbolInfo(assignment.Left, cancellationToken).Symbol as ILocalSymbol,
            _ => null
        };
    }
}
