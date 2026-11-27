using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128268: A symmetric algorithm key is set from a text encoding. The key carries the
///     low entropy and the structure of the source text, with no salt and no work factor.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WeakSymmetricKeySourceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128268";

    private const string SymmetricAlgorithmMetadataName = "System.Security.Cryptography.SymmetricAlgorithm";

    private const string EncodingMetadataName = "System.Text.Encoding";

    private const string KeyPropertyName = "Key";

    private const string GetBytesName = "GetBytes";

    private static readonly string[] EncodingNames =
    [
        "UTF8",
        "ASCII",
        "Unicode",
        "UTF32",
        "Latin1",
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Symmetric key is set from a text encoding",
        "Symmetric key is set from 'Encoding.{0}': use a random generator or a key derivation function",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A text encoding carries the low entropy and the structure of the source text, with no salt and no work factor. Use a random generator or a key derivation function.");

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
            var symmetricAlgorithm = start.Compilation.GetTypeByMetadataName(SymmetricAlgorithmMetadataName);
            var encoding = start.Compilation.GetTypeByMetadataName(EncodingMetadataName);

            if (symmetricAlgorithm is null || encoding is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, symmetricAlgorithm, encoding),
                SyntaxKind.SimpleAssignmentExpression);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol symmetricAlgorithm,
        INamedTypeSymbol encoding)
    {
        if (context.Node is not AssignmentExpressionSyntax assignment
            || assignment.Left is not MemberAccessExpressionSyntax left
            || !string.Equals(left.Name.Identifier.ValueText, KeyPropertyName, StringComparison.Ordinal)
            || context.SemanticModel.GetSymbolInfo(left, context.CancellationToken).Symbol is not IPropertySymbol property
            || !TypeSymbols.DerivesFrom(property.ContainingType, symmetricAlgorithm)
            || !TryGetEncodingSource(assignment.Right, context, encoding, out var encodingName))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation(), encodingName));
    }

    private static bool TryGetEncodingSource(
        ExpressionSyntax expression,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol encoding,
        out string encodingName)
    {
        encodingName = string.Empty;

        if (expression is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: GetBytesName,
                    Expression: MemberAccessExpressionSyntax source
                }
            })
        {
            return false;
        }

        if (!IsEncodingName(source.Name.Identifier.ValueText)
            || context.SemanticModel.GetTypeInfo(source.Expression, context.CancellationToken).Type is not { } sourceType
            || !SymbolEqualityComparer.Default.Equals(sourceType, encoding))
        {
            return false;
        }

        encodingName = source.Name.Identifier.ValueText;
        return true;
    }

    private static bool IsEncodingName(string name)
    {
        foreach (var candidate in EncodingNames)
        {
            if (string.Equals(name, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
