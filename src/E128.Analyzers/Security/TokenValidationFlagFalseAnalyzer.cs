using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128228: Reports a <c lang="csharp">TokenValidationParameters</c> flag set to a literal
///     <c lang="csharp">false</c>, which turns a JWT check off with no build warning.
/// </summary>
/// <remarks>
///     Five flags default to <c lang="csharp">true</c>, so a literal <c lang="csharp">false</c>
///     silently disables the check. <c lang="csharp">ValidateIssuerSigningKey</c> defaults to
///     <c lang="csharp">false</c>, so the rule reports it with a distinct message. The rule
///     resolves the property symbol against <c lang="csharp">TokenValidationParameters</c> and
///     reports only a literal <c lang="csharp">false</c>, so a value read from configuration stays
///     silent. The safe value depends on the deployment, so the rule carries no code fix.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TokenValidationFlagFalseAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128228";

    private const string TokenValidationParametersTypeName = "Microsoft.IdentityModel.Tokens.TokenValidationParameters";

    private const string SigningKeyFlagName = "ValidateIssuerSigningKey";

    private static readonly ImmutableHashSet<string> DisabledCheckNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ValidateIssuer",
        "ValidateAudience",
        "ValidateLifetime",
        "RequireExpirationTime",
        "RequireSignedTokens");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Token validation flag set to constant false",
        "JWT check '{0}' is set to false, so the token handler skips it",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "The safe value depends on the deployment. Set the flag from configuration, or set it to true.");

    private static readonly DiagnosticDescriptor SigningKeyRule = new(
        DiagnosticId,
        "Issuer signing-key validation left off",
        "ValidateIssuerSigningKey is set to false, which is the default, so the token handler does not check that the signing key belongs to the issuer",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "Set the flag to true when the issuer publishes its signing keys.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, SigningKeyRule];

    /// <inheritdoc />
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
            MetadataTypes.RegisterForType(
                startContext,
                TokenValidationParametersTypeName,
                Analyze,
                SyntaxKind.SimpleAssignmentExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol parametersType)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
                is not IPropertySymbol property
            || !TypeSymbols.DerivesFrom(property.ContainingType, parametersType))
        {
            return;
        }

        var isSigningKey = string.Equals(property.Name, SigningKeyFlagName, StringComparison.Ordinal);
        if (!isSigningKey && !DisabledCheckNames.Contains(property.Name))
        {
            return;
        }

        if (!IsFalseLiteral(assignment.Right))
        {
            return;
        }

        context.ReportDiagnostic(isSigningKey
            ? Diagnostic.Create(SigningKeyRule, assignment.GetLocation())
            : Diagnostic.Create(Rule, assignment.GetLocation(), property.Name));
    }

    private static bool IsFalseLiteral(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression.IsKind(SyntaxKind.FalseLiteralExpression);
    }
}
