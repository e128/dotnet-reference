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
        "E128-Unsafe",
        DiagnosticSeverity.Warning,
        true,
        "A nonpublic base class library member is an implementation detail. A servicing release " +
        "can rename it, remove it, or change its shape. Use the public API instead.");

    /// <summary>
    ///     Public key tokens of framework assemblies: ECMA (b77a5c561934e089), Microsoft
    ///     (b03f5f7f11d50a3a), Microsoft extensions (adb9793829ddae60), Microsoft product
    ///     (31bf3856ad364e35), and the core library (7cec85d7bea7798e).
    /// </summary>
    private static readonly byte[][] FrameworkPublicKeyTokens =
    [
        [0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89],
        [0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a],
        [0xad, 0xb9, 0x79, 0x38, 0x29, 0xdd, 0xae, 0x60],
        [0x31, 0xbf, 0x38, 0x56, 0xad, 0x36, 0x4e, 0x35],
        [0x7c, 0xec, 0x85, 0xd7, 0xbe, 0xa7, 0x79, 0x8e],
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
        var identity = type.ContainingAssembly?.Identity;

        // The name prefix alone also selects a user assembly named System.Foo. The Microsoft public
        // key token proves the assembly ships with the framework. An unsigned assembly has none.
        return identity is not null
            && (identity.Name.StartsWith("System", StringComparison.Ordinal)
                || identity.Name.StartsWith("Microsoft", StringComparison.Ordinal))
            && HasFrameworkPublicKeyToken(identity.PublicKeyToken);
    }

    private static bool HasFrameworkPublicKeyToken(ImmutableArray<byte> token)
    {
        if (token.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var known in FrameworkPublicKeyTokens)
        {
            if (token.SequenceEqual(known))
            {
                return true;
            }
        }

        return false;
    }
}
