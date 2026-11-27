using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128266: A certificate chain build discards its result, or its policy drops a
///     verification check. Either form accepts a certificate the chain would reject.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CertificateChainBypassAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128266";

    private const string ChainMetadataName = "System.Security.Cryptography.X509Certificates.X509Chain";

    private const string PolicyMetadataName = "System.Security.Cryptography.X509Certificates.X509ChainPolicy";

    private const string FlagsMetadataName = "System.Security.Cryptography.X509Certificates.X509VerificationFlags";

    private const string RevocationMetadataName = "System.Security.Cryptography.X509Certificates.X509RevocationMode";

    private const string BuildName = "Build";

    private const string VerificationFlagsName = "VerificationFlags";

    private const string RevocationModeName = "RevocationMode";

    private const string NoCheckName = "NoCheck";

    private static readonly string[] WeakeningFlags =
    [
        "AllowUnknownCertificateAuthority",
        "IgnoreNotTimeValid",
        "IgnoreWrongUsage",
        "IgnoreCtlNotTimeValid",
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Certificate chain build result is ignored or its policy is weakened",
        "{0}: check the chain result and keep every verification check enabled",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A certificate chain that discards its Build result, or whose policy ignores a check, accepts a certificate the chain would reject. Check the result and keep every verification check enabled.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            var chain = start.Compilation.GetTypeByMetadataName(ChainMetadataName);

            if (chain is null)
            {
                return;
            }

            var policy = start.Compilation.GetTypeByMetadataName(PolicyMetadataName);
            var flags = start.Compilation.GetTypeByMetadataName(FlagsMetadataName);
            var revocation = start.Compilation.GetTypeByMetadataName(RevocationMetadataName);

            start.RegisterSyntaxNodeAction(
                node => AnalyzeInvocation(node, chain),
                SyntaxKind.InvocationExpression);

            start.RegisterSyntaxNodeAction(
                node => AnalyzeAssignment(node, policy, flags, revocation),
                SyntaxKind.SimpleAssignmentExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol chain)
    {
        if (context.Node is not InvocationExpressionSyntax invocation
            || invocation.Parent is not ExpressionStatementSyntax
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !string.Equals(method.Name, BuildName, StringComparison.Ordinal)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, chain))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            "The certificate chain Build result is discarded"));
    }

    private static void AnalyzeAssignment(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? policy,
        INamedTypeSymbol? flags,
        INamedTypeSymbol? revocation)
    {
        if (context.Node is not AssignmentExpressionSyntax assignment
            || assignment.Left is not MemberAccessExpressionSyntax access
            || context.SemanticModel.GetSymbolInfo(access, context.CancellationToken).Symbol is not IPropertySymbol property
            || policy is null
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, policy))
        {
            return;
        }

        var message = BypassMessage(assignment, property, context, flags, revocation);

        if (message is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation(), message));
    }

    private static string? BypassMessage(
        AssignmentExpressionSyntax assignment,
        IPropertySymbol property,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? flags,
        INamedTypeSymbol? revocation)
    {
        return property.Name switch
        {
            VerificationFlagsName => WeakenedFlags(assignment.Right, context, flags) is { } weakened
                ? $"The chain policy ignores the '{weakened}' check"
                : null,
            RevocationModeName => IsNoCheck(assignment.Right, context, revocation)
                ? "The chain policy disables revocation checking"
                : null,
            _ => null,
        };
    }

    private static string? WeakenedFlags(
        ExpressionSyntax expression,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? flags)
    {
        if (flags is null)
        {
            return null;
        }

        var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);

        if (!constant.HasValue || constant.Value is not int value)
        {
            return null;
        }

        return Array.Find(
            WeakeningFlags,
            name => FlagValue(flags, name) is { } flag && (value & flag) is not 0);
    }

    private static bool IsNoCheck(
        ExpressionSyntax expression,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? revocation)
    {
        if (revocation is null || FlagValue(revocation, NoCheckName) is not { } noCheck)
        {
            return false;
        }

        var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);

        return constant.HasValue && constant.Value is int value && value == noCheck;
    }

    private static int? FlagValue(INamedTypeSymbol type, string name)
    {
        foreach (var member in type.GetMembers(name).OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue && member.ConstantValue is int value)
            {
                return value;
            }
        }

        return null;
    }
}
