using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128141: <c lang="csharp">Encoding.UTF8.GetBytes</c> on a constant string runs the encoder
///     on every call. A <c lang="csharp">u8</c> literal stores the bytes in the assembly.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Utf8GetBytesLiteralAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128141";
    internal const string UseToArrayKey = "UseToArray";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use a u8 literal instead of Encoding.UTF8.GetBytes",
        "Encoding.UTF8.GetBytes encodes a constant string on every call. A u8 literal is stored in the assembly.",
        "Performance",
        DiagnosticSeverity.Warning,
        true);

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
            var encoding = start.Compilation.GetTypeByMetadataName("System.Text.Encoding");
            var readOnlySpanType = start.Compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
            var byteType = start.Compilation.GetSpecialType(SpecialType.System_Byte);

            if (encoding is null
                || readOnlySpanType is null
                || byteType.TypeKind == TypeKind.Error)
            {
                return;
            }

            var readOnlySpanByte = readOnlySpanType.Construct(byteType);

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, encoding, readOnlySpanByte),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol encoding,
        INamedTypeSymbol readOnlySpanByte)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "GetBytes",
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "UTF8" } utf8Access,
            }
            || invocation.ArgumentList.Arguments.Count != 1
            || invocation.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        var model = context.SemanticModel;

        if (model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol
            {
                Parameters.Length: 1,
            } method
            || !string.Equals(method.Name, "GetBytes", StringComparison.Ordinal)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, encoding)
            || method.Parameters[0].Type.SpecialType != SpecialType.System_String
            || model.GetSymbolInfo(utf8Access, context.CancellationToken).Symbol is not IPropertySymbol property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, encoding))
        {
            return;
        }

        if (!SupportsUtf8Literals(invocation))
        {
            return;
        }

        var useToArray = !TargetsByteSpan(invocation, model, readOnlySpanByte, context.CancellationToken);

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            ImmutableDictionary.CreateRange(
                [new KeyValuePair<string, string?>(UseToArrayKey, useToArray ? "true" : "false")])));
    }

    internal static bool SupportsUtf8Literals(SyntaxNode node)
    {
        return node.SyntaxTree.Options is CSharpParseOptions options
            && options.LanguageVersion.MapSpecifiedToEffectiveVersion() >= LanguageVersion.CSharp11;
    }

    // A u8 literal is a ReadOnlySpan<byte>, so it converts to that type alone. A Span<byte>
    // target takes the byte[] shape through the implicit array conversion.
    private static bool TargetsByteSpan(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        INamedTypeSymbol readOnlySpanByte,
        CancellationToken cancellationToken)
    {
        if (invocation.Parent is ArgumentSyntax argument)
        {
            var target = argument.Parent switch
            {
                ArgumentListSyntax { Parent: InvocationExpressionSyntax call } =>
                    model.GetSymbolInfo(call, cancellationToken).Symbol as IMethodSymbol,
                ArgumentListSyntax { Parent: BaseObjectCreationExpressionSyntax creation } =>
                    model.GetSymbolInfo(creation, cancellationToken).Symbol as IMethodSymbol,
                _ => null,
            };

            return FindParameter(target, argument) is { } parameter
                && SymbolEqualityComparer.Default.Equals(parameter.Type, readOnlySpanByte);
        }

        return invocation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }
            && declarator.Parent is VariableDeclarationSyntax declaration
            && SymbolEqualityComparer.Default.Equals(
                model.GetTypeInfo(declaration.Type, cancellationToken).Type,
                readOnlySpanByte);
    }

    private static IParameterSymbol? FindParameter(IMethodSymbol? target, ArgumentSyntax argument)
    {
        if (target is null)
        {
            return null;
        }

        if (argument.NameColon?.Name.Identifier.ValueText is { } name)
        {
            foreach (var parameter in target.Parameters)
            {
                if (string.Equals(parameter.Name, name, StringComparison.Ordinal))
                {
                    return parameter;
                }
            }

            return null;
        }

        if (argument.Parent is not ArgumentListSyntax list)
        {
            return null;
        }

        // A named sibling makes the written position unreliable, so keep the byte[] shape.
        foreach (var sibling in list.Arguments)
        {
            if (sibling.NameColon is not null)
            {
                return null;
            }
        }

        var position = list.Arguments.IndexOf(argument);
        return position >= 0 && position < target.Parameters.Length ? target.Parameters[position] : null;
    }
}
