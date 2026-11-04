using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128156: Reports a <c lang="csharp">HttpClientHandler</c> certificate validation callback
///     that accepts every certificate. The handler then trusts any server, so transport security
///     gives no identity guarantee.
/// </summary>
/// <remarks>
///     CA5359 covers the <c lang="csharp">ServicePointManager</c> callback only, so the
///     <c lang="csharp">HttpClientHandler</c> path stays open. The rule reports an unconditional
///     acceptance only. A callback that inspects the chain or the policy errors stays silent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpClientCertificateBypassAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128156";

    private const string HandlerMetadataName = "System.Net.Http.HttpClientHandler";

    private const string CallbackPropertyName = "ServerCertificateCustomValidationCallback";

    private const string SslOptionsMetadataName = "System.Net.Security.SslClientAuthenticationOptions";

    private const string SslStreamMetadataName = "System.Net.Security.SslStream";

    private const string SslCallbackPropertyName = "RemoteCertificateValidationCallback";

    private const string SslStreamCallbackParameterName = "userCertificateValidationCallback";

    private const string DangerousValidatorName = "DangerousAcceptAnyServerCertificateValidator";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "HttpClient certificate validation accepts every certificate",
        "Certificate validation accepts every certificate",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "A ServerCertificateCustomValidationCallback that returns true for every certificate " +
        "disables transport identity checks. An attacker on the network path can present any " +
        "certificate and read or alter the traffic.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var handlerTypes = MetadataTypes.Find(context.Compilation, HandlerMetadataName);
        var sslOptionsTypes = MetadataTypes.Find(context.Compilation, SslOptionsMetadataName);
        var sslStreamTypes = MetadataTypes.Find(context.Compilation, SslStreamMetadataName);

        if (handlerTypes.IsEmpty && sslOptionsTypes.IsEmpty && sslStreamTypes.IsEmpty)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeAssignment(ctx, handlerTypes, sslOptionsTypes),
            SyntaxKind.SimpleAssignmentExpression);

        if (!sslStreamTypes.IsEmpty)
        {
            context.RegisterSyntaxNodeAction(
                ctx => AnalyzeSslStreamCreation(ctx, sslStreamTypes),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        }
    }

    private static void AnalyzeSslStreamCreation(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> sslStreamTypes)
    {
        if (context.SemanticModel.GetOperation(context.Node, context.CancellationToken)
            is not IObjectCreationOperation creation
            || !MetadataTypes.Contains(sslStreamTypes, creation.Constructor?.ContainingType))
        {
            return;
        }

        foreach (var argument in creation.Arguments)
        {
            if (argument.Parameter is { Name: SslStreamCallbackParameterName }
                && argument.Value.Syntax is ExpressionSyntax value
                && AcceptsEveryCertificate(
                    value,
                    context.SemanticModel,
                    depth: 0,
                    context.CancellationToken))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation()));
                return;
            }
        }
    }

    private static void AnalyzeAssignment(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> handlerTypes,
        ImmutableArray<INamedTypeSymbol> sslOptionsTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (GetAssignedMemberName(assignment.Left) is not { } memberName
            || (!string.Equals(memberName, CallbackPropertyName, StringComparison.Ordinal)
                && !string.Equals(memberName, SslCallbackPropertyName, StringComparison.Ordinal)))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
            is not IPropertySymbol property
            || !(MetadataTypes.Contains(handlerTypes, property.ContainingType)
                 || MetadataTypes.Contains(sslOptionsTypes, property.ContainingType)))
        {
            return;
        }

        if (AcceptsEveryCertificate(
                assignment.Right,
                context.SemanticModel,
                depth: 0,
                context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation()));
        }
    }

    private static string? GetAssignedMemberName(ExpressionSyntax left)
    {
        return left switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };
    }

    private static bool AcceptsEveryCertificate(
        ExpressionSyntax value,
        SemanticModel model,
        int depth,
        CancellationToken cancellationToken)
    {
        var unwrapped = ExpressionUnwrapper.Parentheses(value);

        if (IsAlwaysTrue(unwrapped, model, cancellationToken))
        {
            return true;
        }

        if (unwrapped is MemberAccessExpressionSyntax memberAccess
            && string.Equals(
                memberAccess.Name.Identifier.ValueText,
                DangerousValidatorName,
                StringComparison.Ordinal))
        {
            // HttpClientHandler.DangerousAcceptAnyServerCertificateValidator is the framework's own
            // name for the unconditional callback. The name alone is enough, because the property
            // has no other meaning.
            return true;
        }

        if (unwrapped is AnonymousFunctionExpressionSyntax function)
        {
            return function.Body switch
            {
                ExpressionSyntax expressionBody => AcceptsEveryCertificate(
                    expressionBody, model, depth + 1, cancellationToken),
                BlockSyntax block => BlockReturnsTrue(block, model, cancellationToken),
                _ => false
            };
        }

        // A delegate held by a method, a local, or a field hides the body behind a name. One hop
        // through the declaration decides the case, and a longer chain stays silent.
        return depth < 1
               && GetSource(unwrapped, model, cancellationToken) is { } source
               && IsUnconditionalTrue(source, model, cancellationToken);
    }

    /// <summary>
    ///     Returns the expression a name stands for. A local or a field yields its initializer, and a
    ///     method yields its body.
    /// </summary>
    private static CSharpSyntaxNode? GetSource(
        ExpressionSyntax value,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(value, cancellationToken).Symbol;

        if (symbol is null)
        {
            return null;
        }

        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (GetDeclarationSource(reference.GetSyntax(cancellationToken)) is { } source)
            {
                return source;
            }
        }

        return null;
    }

    private static CSharpSyntaxNode? GetDeclarationSource(SyntaxNode declaration)
    {
        return declaration switch
        {
            VariableDeclaratorSyntax { Initializer.Value: { } initializer } => initializer,
            MethodDeclarationSyntax { ExpressionBody.Expression: { } expression } => expression,
            MethodDeclarationSyntax { Body: { } body } => body,
            LocalFunctionStatementSyntax { ExpressionBody.Expression: { } expression } => expression,
            LocalFunctionStatementSyntax { Body: { } body } => body,
            _ => null
        };
    }

    private static bool IsUnconditionalTrue(
        CSharpSyntaxNode source,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return source switch
        {
            BlockSyntax block => BlockReturnsTrue(block, model, cancellationToken),
            ExpressionSyntax expression => AcceptsEveryCertificate(
                expression, model, depth: 1, cancellationToken: cancellationToken),
            _ => false
        };
    }

    private static bool BlockReturnsTrue(
        BlockSyntax block,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        // A block with one return statement is the only block shape the rule can decide. Any extra
        // statement can reject a certificate, and the rule cannot read its intent.
        return block.Statements.Count == 1
               && block.Statements[0] is ReturnStatementSyntax { Expression: { } returned }
               && IsAlwaysTrue(ExpressionUnwrapper.Parentheses(returned), model, cancellationToken);
    }

    /// <summary>
    ///     Returns true when the expression is a constant <c lang="csharp">true</c>, or an
    ///     <c lang="csharp">||</c> with one side that is always true. The right side then decides the
    ///     result alone, whatever the other side holds.
    /// </summary>
    private static bool IsAlwaysTrue(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var unwrapped = ExpressionUnwrapper.Parentheses(expression);

        return model.GetConstantValue(unwrapped, cancellationToken).Value is true
               || (unwrapped is BinaryExpressionSyntax binary
                   && binary.IsKind(SyntaxKind.LogicalOrExpression)
                   && (IsAlwaysTrue(binary.Left, model, cancellationToken)
                       || IsAlwaysTrue(binary.Right, model, cancellationToken)));
    }
}
