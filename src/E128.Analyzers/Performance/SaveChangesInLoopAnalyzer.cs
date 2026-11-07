using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128185: Reports an Entity Framework Core <c lang="csharp">SaveChanges</c> or
///     <c lang="csharp">SaveChangesAsync</c> call inside a loop body. The call runs once per
///     iteration, so the loop pays one database round trip per row.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SaveChangesInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128185";

    private const string DbContextMetadataName = "Microsoft.EntityFrameworkCore.DbContext";

    private const string EntityFrameworkNamespacePrefix = "Microsoft.EntityFrameworkCore";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Entity Framework Core SaveChanges inside a loop",
        "'{0}' runs once per iteration, which costs one database round trip per row",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

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
            // Fail closed. A compilation without Entity Framework Core cannot contain the
            // call this rule describes, and a look-alike type named DbContext is not EF.
            var dbContextType = startContext.Compilation.GetTypeByMetadataName(DbContextMetadataName);
            if (dbContextType is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, dbContextType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol dbContextType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic gate first. Most invocations are not SaveChanges, so they must not pay
        // for loop walking or overload resolution.
        var syntaxName = GetMethodName(invocation);
        if (!IsSaveChangesName(syntaxName))
        {
            return;
        }

        if (!LoopContext.IsInsideLoopBody(invocation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var receiverType = GetReceiverType(invocation, context.SemanticModel, context.CancellationToken);

        if (!IsEfSaveChanges(method, receiverType, dbContextType))
        {
            return;
        }

        // A null-conditional call reports the whole conditional access, so the location covers the
        // receiver that may be null.
        var location = invocation.Parent is ConditionalAccessExpressionSyntax conditionalAccess
            ? conditionalAccess.GetLocation()
            : invocation.GetLocation();

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, method.Name));
    }

    private static string GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => string.Empty
        };
    }

    /// <summary>
    ///     Returns the type of the receiver. A member binding names no receiver of its own, so the
    ///     receiver comes from the enclosing conditional access.
    /// </summary>
    private static ITypeSymbol? GetReceiverType(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess =>
                model.GetTypeInfo(memberAccess.Expression, cancellationToken).Type,
            MemberBindingExpressionSyntax when invocation.Parent is ConditionalAccessExpressionSyntax conditionalAccess =>
                model.GetTypeInfo(conditionalAccess.Expression, cancellationToken).Type,
            _ => null
        };
    }

    private static bool IsSaveChangesName(string methodName)
    {
        return string.Equals(methodName, "SaveChanges", StringComparison.Ordinal)
               || string.Equals(methodName, "SaveChangesAsync", StringComparison.Ordinal);
    }

    private static bool IsEfSaveChanges(
        IMethodSymbol method,
        ITypeSymbol? receiverType,
        INamedTypeSymbol dbContextType)
    {
        // The declaring type covers an instance call, including a call made through a
        // derived DbContext, because the walk climbs the base chain.
        if (DerivesFromDbContext(method.ContainingType, dbContextType))
        {
            return true;
        }

        // A derived receiver still reports when overload resolution binds the call to a
        // base declaration that the analyzer cannot walk, such as a type parameter bound.
        if (receiverType is INamedTypeSymbol receiver && DerivesFromDbContext(receiver, dbContextType))
        {
            return true;
        }

        if (!method.IsExtensionMethod)
        {
            return false;
        }

        // EF Core also ships extension methods. The containing type carries the EF
        // namespace or assembly, and the receiver can name an unbound generic EF type.
        return IsEntityFrameworkType(method.ContainingType) || IsEntityFrameworkType(receiverType);
    }

    private static bool DerivesFromDbContext(INamedTypeSymbol? type, INamedTypeSymbol dbContextType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, dbContextType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEntityFrameworkType(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        var definition = type.OriginalDefinition;

        var namespaceName = definition.ContainingNamespace?.ToDisplayString();
        if (namespaceName is not null
            && namespaceName.StartsWith(EntityFrameworkNamespacePrefix, StringComparison.Ordinal))
        {
            return true;
        }

        var assemblyName = definition.ContainingAssembly?.Name;
        return assemblyName is not null
               && assemblyName.StartsWith(EntityFrameworkNamespacePrefix, StringComparison.Ordinal);
    }
}
