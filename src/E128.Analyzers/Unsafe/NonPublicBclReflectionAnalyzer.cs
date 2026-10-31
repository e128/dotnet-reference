using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     E128137: Reflection reads or writes a nonpublic member of a base class library type. The
///     member can change in any servicing release.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonPublicBclReflectionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128137";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not reach a nonpublic BCL member",
        "Do not read or write a nonpublic member of '{0}'. It is an implementation detail.",
        "Unsafe",
        DiagnosticSeverity.Warning,
        true,
        "A nonpublic base class library member is an implementation detail. A servicing release " +
        "can rename it, remove it, or change its shape. Use the public API instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var type = start.Compilation.GetTypeByMetadataName("System.Type");
            var bindingFlags = start.Compilation.GetTypeByMetadataName("System.Reflection.BindingFlags");

            if (type is null || bindingFlags is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, type, bindingFlags),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol systemType,
        INamedTypeSymbol bindingFlags)
    {
        if (context.Node is not InvocationExpressionSyntax invocation
            || invocation.ArgumentList is null)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !DerivesFromSystemType(method.ContainingType, systemType)
            || !IsMemberLookup(method.Name))
        {
            return;
        }

        if (!UsesNonPublicFlag(invocation, context.SemanticModel, bindingFlags, context.CancellationToken))
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: TypeOfExpressionSyntax typeOf }
            || context.SemanticModel.GetTypeInfo(typeOf.Type, context.CancellationToken).Type is not { } target
            || !IsFrameworkType(target))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            target.ToDisplayString()));
    }

    private static bool DerivesFromSystemType(INamedTypeSymbol? type, INamedTypeSymbol systemType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, systemType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMemberLookup(string name)
    {
        return name is "GetMethod"
            or "GetField"
            or "GetProperty"
            or "GetEvent"
            or "GetMember"
            or "GetConstructor"
            or "GetNestedType";
    }

    private static bool UsesNonPublicFlag(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        INamedTypeSymbol bindingFlags,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            foreach (var node in argument.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                if (!string.Equals(node.Identifier.ValueText, "NonPublic", StringComparison.Ordinal))
                {
                    continue;
                }

                if (model.GetSymbolInfo(node, cancellationToken).Symbol is IFieldSymbol field
                    && SymbolEqualityComparer.Default.Equals(field.ContainingType, bindingFlags))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsFrameworkType(ITypeSymbol type)
    {
        var assemblyName = type.ContainingAssembly?.Identity.Name;

        return assemblyName is not null
            && (assemblyName.StartsWith("System", StringComparison.Ordinal)
                || assemblyName.StartsWith("Microsoft", StringComparison.Ordinal));
    }
}
