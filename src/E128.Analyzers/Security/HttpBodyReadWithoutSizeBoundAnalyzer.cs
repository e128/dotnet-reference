using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128227: Reports an <c lang="csharp">HttpContent</c> body read with no size bound.
///     <c lang="csharp">ReadAsStringAsync</c> and <c lang="csharp">ReadAsByteArrayAsync</c> buffer
///     the whole body in memory, so an untrusted server can exhaust the heap.
/// </summary>
/// <remarks>
///     The rule reads the enclosing method body and the enclosing type. A
///     <c lang="csharp">ContentLength</c> check or a
///     <c lang="csharp">LoadIntoBufferAsync</c> call in the method bounds the read. A
///     <c lang="csharp">MaxResponseContentBufferSize</c> setting anywhere in the type bounds it too.
///     An in-memory content type needs no bound, because its size is already known. The rule carries
///     no code fix, because the cap value is a caller decision.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpBodyReadWithoutSizeBoundAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128227";

    private const string HttpContentTypeName = "System.Net.Http.HttpContent";

    private const string ByteArrayContentTypeName = "System.Net.Http.ByteArrayContent";

    private const string ReadOnlyMemoryContentTypeName = "System.Net.Http.ReadOnlyMemoryContent";

    private const string JsonContentTypeName = "System.Net.Http.Json.JsonContent";

    private static readonly ImmutableHashSet<string> ReadMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ReadAsStringAsync",
        "ReadAsByteArrayAsync");

    private static readonly ImmutableHashSet<string> MethodBoundNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ContentLength",
        "LoadIntoBufferAsync");

    private static readonly ImmutableHashSet<string> TypeBoundNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "MaxResponseContentBufferSize");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "HTTP body read without a size bound",
        "'{0}' buffers the whole body with no size bound. Check ContentLength, call LoadIntoBufferAsync, or set MaxResponseContentBufferSize first.",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true);

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

        context.RegisterCompilationStartAction(static startContext =>
        {
            // The rule names an HttpContent member. Without that type there is no report to make.
            if (startContext.Compilation.GetTypeByMetadataName(HttpContentTypeName) is not { } httpContent)
            {
                return;
            }

            INamedTypeSymbol?[] inMemoryContents =
            [
                startContext.Compilation.GetTypeByMetadataName(ByteArrayContentTypeName),
                startContext.Compilation.GetTypeByMetadataName(ReadOnlyMemoryContentTypeName),
                startContext.Compilation.GetTypeByMetadataName(JsonContentTypeName)
            ];

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeRead(nodeContext, httpContent, inMemoryContents),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeRead(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol httpContent,
        INamedTypeSymbol?[] inMemoryContents)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !ReadMethodNames.Contains(access.Name.Identifier.ValueText))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || !TypeSymbols.DerivesFrom(method.ContainingType, httpContent))
        {
            return;
        }

        var receiverType = model.GetTypeInfo(access.Expression, cancellationToken).Type;
        if (IsInMemoryContent(receiverType, inMemoryContents) || HasSizeBound(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), method.Name));
    }

    private static bool IsInMemoryContent(ITypeSymbol? type, INamedTypeSymbol?[] inMemoryContents)
    {
        foreach (var content in inMemoryContents)
        {
            if (TypeSymbols.DerivesFrom(type, content))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when a bound sits in the enclosing method body, or when the enclosing type
    ///     configures the client cap. A bound in another method cannot be proven to apply to this
    ///     read, so only the client cap is trusted at type scope.
    /// </summary>
    private static bool HasSizeBound(InvocationExpressionSyntax read)
    {
        if (BlockScope.GetScope(read) is { } methodScope && Mentions(methodScope, MethodBoundNames))
        {
            return true;
        }

        return read.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } type
               && Mentions(type, TypeBoundNames);
    }

    private static bool Mentions(SyntaxNode scope, ImmutableHashSet<string> names)
    {
        return scope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(name => names.Contains(name.Identifier.ValueText));
    }
}
