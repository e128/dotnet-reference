using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128252: <c lang="csharp">Convert.ToBase64String</c> or
///     <c lang="csharp">Convert.FromBase64String</c> allocates a string that a span API can
///     avoid.
/// </summary>
/// <remarks>
///     <c lang="csharp">Base64.EncodeToUtf8</c> and <c lang="csharp">Base64.DecodeFromUtf8</c>
///     write into a caller buffer and allocate nothing. The rule gates on the
///     <c lang="csharp">System.Buffers.Text.Base64</c> type, so a framework without the span API
///     gets no report. The rule carries no code fix, because the destination buffer and its
///     length check belong to the caller.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Base64SpanApiAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128252";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Base64 conversion allocates a string",
        "Use Base64.EncodeToUtf8 or Base64.DecodeFromUtf8 instead of Convert.{0}",
        "E128-Performance",
        DiagnosticSeverity.Info,
        true,
        "The span API writes into a caller buffer and allocates nothing. Convert returns a new string.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
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
            if (start.Compilation.GetTypeByMetadataName("System.Buffers.Text.Base64") is null)
            {
                return;
            }

            var convert = start.Compilation.GetTypeByMetadataName("System.Convert");
            if (convert is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, convert), SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol convert)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, convert)
            || !IsConvertedMember(method.Name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), method.Name));
    }

    private static bool IsConvertedMember(string name)
    {
        return string.Equals(name, "ToBase64String", StringComparison.Ordinal)
            || string.Equals(name, "FromBase64String", StringComparison.Ordinal);
    }
}
