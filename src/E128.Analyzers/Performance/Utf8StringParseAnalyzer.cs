using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128131: A <c lang="csharp">Parse</c> or <c lang="csharp">TryParse</c> call reads a string
///     built by <c lang="csharp">Encoding.UTF8.GetString</c>. The UTF-8 overload parses the bytes
///     directly and allocates no string.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Utf8StringParseAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128131";
    internal const string AddProviderKey = "AddProvider";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Parse UTF-8 bytes without a string",
        "'{0}' reads a string that Encoding.UTF8.GetString allocates. Pass the bytes to the UTF-8 overload.",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "The UTF-8 Parse and TryParse overloads read a ReadOnlySpan<byte> directly. The string " +
        "that Encoding.UTF8.GetString builds is an extra allocation on every call.");

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
            var encoding = start.Compilation.GetTypeByMetadataName("System.Text.Encoding");
            var readOnlySpan = start.Compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
            var formatProvider = start.Compilation.GetTypeByMetadataName("System.IFormatProvider");
            var byteType = start.Compilation.GetSpecialType(SpecialType.System_Byte);

            if (encoding is null
                || readOnlySpan is null
                || formatProvider is null
                || byteType.TypeKind is TypeKind.Error)
            {
                return;
            }

            var spanByte = readOnlySpan.Construct(byteType);
            var context = new ParseContext(encoding, spanByte, formatProvider);

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, context),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ParseContext parseContext)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var model = context.SemanticModel;

        if (GetParseMethod(invocation, model, context.CancellationToken) is not { } method
            || GetUtf8GetStringCall(invocation, method, model, parseContext, context.CancellationToken) is not { } getString
            || !TryFindOverload(method, parseContext, out var addProvider))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            getString.GetLocation(),
            ImmutableDictionary.CreateRange(
                [
                    new KeyValuePair<string, string?>(AddProviderKey, addProvider ? "true" : "false")
                ]),
            method.Name));
    }

    // Returns the method when the call is a string Parse or TryParse overload.
    private static IMethodSymbol? GetParseMethod(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { IsGenericMethod: false } method
            && method.Parameters.Length is not 0
            && method.Parameters[0].Type.SpecialType is SpecialType.System_String
            && (string.Equals(method.Name, "Parse", StringComparison.Ordinal)
                || string.Equals(method.Name, "TryParse", StringComparison.Ordinal))
            ? method
            : null;
    }

    // Returns the Encoding.UTF8.GetString call that feeds the first argument, when its source is bytes.
    private static InvocationExpressionSyntax? GetUtf8GetStringCall(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel model,
        ParseContext parseContext,
        System.Threading.CancellationToken cancellationToken)
    {
        if (invocation.ArgumentList is null
            || invocation.ArgumentList.Arguments.Count != method.Parameters.Length
            || invocation.ArgumentList.Arguments[0].Expression is not InvocationExpressionSyntax getString
            || !IsUtf8GetString(getString, model, parseContext.Encoding, cancellationToken)
            || getString.ArgumentList.Arguments.Count is not 1
            || !IsByteSource(getString.ArgumentList.Arguments[0].Expression, model, parseContext, cancellationToken))
        {
            return null;
        }

        return getString;
    }

    private static bool IsUtf8GetString(
        InvocationExpressionSyntax getString,
        SemanticModel model,
        INamedTypeSymbol encoding,
        System.Threading.CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(getString, cancellationToken).Symbol is IMethodSymbol method
            && string.Equals(method.Name, "GetString", StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, encoding)
            && getString.Expression is MemberAccessExpressionSyntax memberAccess
            && model.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol is IPropertySymbol property
            && string.Equals(property.Name, "UTF8", StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(property.ContainingType, encoding);
    }

    private static bool IsByteSource(
        ExpressionSyntax expression,
        SemanticModel model,
        ParseContext parseContext,
        System.Threading.CancellationToken cancellationToken)
    {
        var type = model.GetTypeInfo(expression, cancellationToken).Type;

        return type is IArrayTypeSymbol array
            ? array.ElementType.SpecialType is SpecialType.System_Byte
            : type is INamedTypeSymbol named
                && SymbolEqualityComparer.Default.Equals(named, parseContext.SpanByte);
    }

    internal static bool TryFindOverload(IMethodSymbol method, ParseContext parseContext, out bool addProvider)
    {
        List<ITypeSymbol> expected = [parseContext.SpanByte];

        for (var index = 1; index < method.Parameters.Length; index++)
        {
            expected.Add(method.Parameters[index].Type);
        }

        if (HasOverload(method, expected))
        {
            addProvider = false;
            return true;
        }

        expected.Add(parseContext.FormatProvider);

        if (HasOverload(method, expected))
        {
            addProvider = true;
            return true;
        }

        addProvider = false;
        return false;
    }

    private static bool HasOverload(IMethodSymbol method, List<ITypeSymbol> expected)
    {
        foreach (var member in method.ContainingType.GetMembers(method.Name))
        {
            if (member is not IMethodSymbol candidate
                || candidate.IsGenericMethod
                || candidate.Parameters.Length != expected.Count)
            {
                continue;
            }

            var matches = true;

            for (var index = 0; index < expected.Count; index++)
            {
                if (!SymbolEqualityComparer.Default.Equals(candidate.Parameters[index].Type, expected[index]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    internal sealed class ParseContext
    {
        public ParseContext(INamedTypeSymbol encoding, INamedTypeSymbol spanByte, INamedTypeSymbol formatProvider)
        {
            Encoding = encoding;
            SpanByte = spanByte;
            FormatProvider = formatProvider;
        }

        public INamedTypeSymbol Encoding { get; }

        public INamedTypeSymbol SpanByte { get; }

        public INamedTypeSymbol FormatProvider { get; }
    }
}
