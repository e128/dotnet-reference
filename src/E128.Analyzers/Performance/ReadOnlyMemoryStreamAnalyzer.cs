using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128224: Reports a <c lang="csharp">MemoryStream</c> that wraps a string or a byte array
///     and is read only. <c lang="csharp">StringStream</c> wraps the string without a copy, and
///     <c lang="csharp">ReadOnlyMemoryStream</c> wraps the bytes without a copy.
/// </summary>
/// <remarks>
///     The rule reads one method body and accepts only read-shaped uses. A write call, an unknown
///     consumer, a return, or a store into a field keeps the report silent, because the target
///     types reject a write. The rule carries no fix for the same reason.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadOnlyMemoryStreamAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128224";

    private const string MemoryStreamTypeName = "System.IO.MemoryStream";

    private const string EncodingTypeName = "System.Text.Encoding";

    private static readonly ImmutableHashSet<string> ReadMemberNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Close",
        "CopyTo",
        "CopyToAsync",
        "Dispose",
        "DisposeAsync",
        "Flush",
        "FlushAsync",
        "Read",
        "ReadAsync",
        "ReadAtLeast",
        "ReadAtLeastAsync",
        "ReadByte",
        "ReadExactly",
        "ReadExactlyAsync",
        "Seek");

    private static readonly ImmutableHashSet<string> ReadPropertyNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "CanRead",
        "CanSeek",
        "CanTimeout",
        "Length",
        "Position");

    private static readonly ImmutableHashSet<string> ReadOnlyConsumerCreationTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "BinaryReader",
        "BrotliStream",
        "DeflateStream",
        "GZipStream",
        "StreamContent",
        "StreamReader",
        "XPathDocument");

    private static readonly ImmutableHashSet<string> ReadOnlyConsumerMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ComputeHash",
        "Create",
        "Load",
        "Parse");

    private static readonly ImmutableHashSet<string> ReadOnlyConsumerInvocationTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "HashAlgorithm",
        "JsonDocument",
        "XDocument",
        "XmlReader");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "MemoryStream wraps a read-only source",
        "Use StringStream for a string source or ReadOnlyMemoryStream for a byte array instead of a MemoryStream that only reads",
        "E128-Performance",
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
        context.RegisterSyntaxNodeAction(AnalyzeCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        if (!IsMemoryStreamCreation(creation, model, cancellationToken)
            || !WrapsReadOnlySource(creation, model, cancellationToken))
        {
            return;
        }

        if (GetTargetLocal(creation, model, cancellationToken) is not { } local
            || BlockScope.GetScope(creation) is not { } scope
            || !IsReadOnlyThroughout(scope, local, model, cancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation()));
    }

    private static bool IsMemoryStreamCreation(
        ObjectCreationExpressionSyntax creation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetTypeInfo(creation, cancellationToken).Type is { } type
               && string.Equals(type.ToDisplayString(), MemoryStreamTypeName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     True for a single-argument creation that wraps an encoding result or a byte array. A
    ///     capacity, a slice, or an explicit writable flag stays silent.
    /// </summary>
    private static bool WrapsReadOnlySource(
        ObjectCreationExpressionSyntax creation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (creation.ArgumentList is not { Arguments.Count: 1 } argumentList)
        {
            return false;
        }

        var argument = argumentList.Arguments[0].Expression;

        if (argument is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax access
            && string.Equals(access.Name.Identifier.ValueText, "GetBytes", StringComparison.Ordinal)
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
            && string.Equals(method.ContainingType?.ToDisplayString(), EncodingTypeName, StringComparison.Ordinal))
        {
            return true;
        }

        return model.GetTypeInfo(argument, cancellationToken).Type is IArrayTypeSymbol
        {
            ElementType.SpecialType: SpecialType.System_Byte
        };
    }

    private static ILocalSymbol? GetTargetLocal(
        ObjectCreationExpressionSyntax creation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return creation.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } =>
                model.GetDeclaredSymbol(declarator, cancellationToken) as ILocalSymbol,
            AssignmentExpressionSyntax assignment when assignment.Right == creation
                                                       && assignment.Left is IdentifierNameSyntax =>
                model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol as ILocalSymbol,
            _ => null
        };
    }

    private static bool IsReadOnlyThroughout(
        SyntaxNode scope,
        ILocalSymbol local,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var reference in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (reference.Parent is VariableDeclaratorSyntax
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(reference, cancellationToken).Symbol,
                    local))
            {
                continue;
            }

            if (!IsReadOnlyReference(reference, model, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsReadOnlyReference(
        IdentifierNameSyntax reference,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (reference.Parent is MemberAccessExpressionSyntax access && access.Expression == reference)
        {
            var name = access.Name.Identifier.ValueText;
            return access.Parent is InvocationExpressionSyntax
                ? ReadMemberNames.Contains(name)
                : ReadPropertyNames.Contains(name);
        }

        return reference.Parent is ArgumentSyntax argument
               && IsReadOnlyConsumer(argument, model, cancellationToken);
    }

    private static bool IsReadOnlyConsumer(
        ArgumentSyntax argument,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        switch (argument.Parent?.Parent)
        {
            case BaseObjectCreationExpressionSyntax creation:
                return model.GetTypeInfo(creation, cancellationToken).Type is { } createdType
                       && ReadOnlyConsumerCreationTypeNames.Contains(createdType.Name);

            case InvocationExpressionSyntax invocation:
                return model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
                       && ReadOnlyConsumerMethodNames.Contains(method.Name)
                       && ReadOnlyConsumerInvocationTypeNames.Contains(method.ContainingType?.Name ?? string.Empty);

            default:
                return false;
        }
    }
}
