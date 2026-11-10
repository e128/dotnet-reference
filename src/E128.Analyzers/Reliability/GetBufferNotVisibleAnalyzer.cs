using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128192: Reports a <c lang="csharp">MemoryStream.GetBuffer</c> call whose receiver was created
///     without <c lang="csharp">publiclyVisible: true</c>. The call throws an
///     <c lang="csharp">UnauthorizedAccessException</c> for such a stream.
/// </summary>
/// <remarks>
///     The rule follows a local receiver to its nearest preceding assignment or declaration and a
///     parameter receiver to the nearest simple assignment in the enclosing body. A field, a property,
///     a method result, and a creation outside the body stay silent, because the scan stays block-local. A
///     non-constant <c lang="csharp">publiclyVisible</c> argument also stays silent, since its value
///     is unknown at compile time.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GetBufferNotVisibleAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128192";

    internal const string MemoryStreamTypeName = "System.IO.MemoryStream";

    private const string GetBufferMethodName = "GetBuffer";

    private const string PubliclyVisibleParameterName = "publiclyVisible";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "GetBuffer on a MemoryStream that does not expose its buffer",
        "'GetBuffer' throws for a MemoryStream created without 'publiclyVisible: true'",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "MemoryStream.GetBuffer() throws UnauthorizedAccessException unless the stream was created " +
        "with publiclyVisible set to true. Pass publiclyVisible: true to the constructor, or read the " +
        "bytes with ToArray() or TryGetBuffer() instead.");

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
        // Without the type symbol the rule cannot tell a MemoryStream from a look-alike, so it
        // stays silent rather than guess.
        var memoryStreamType = context.Compilation.GetTypeByMetadataName(MemoryStreamTypeName);

        if (memoryStreamType is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeInvocation(ctx, memoryStreamType),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol memoryStreamType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(
                memberAccess.Name.Identifier.ValueText,
                GetBufferMethodName,
                StringComparison.Ordinal))
        {
            return;
        }

        var model = context.SemanticModel;
        var token = context.CancellationToken;

        // The name alone is not enough. A look-alike type with a GetBuffer member must stay silent,
        // so the symbol has to sit on MemoryStream itself.
        if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method
            || !string.Equals(method.Name, GetBufferMethodName, StringComparison.Ordinal)
            || !IsMemoryStream(method.ContainingType, memoryStreamType))
        {
            return;
        }

        var body = EnclosingBody.Find(invocation);

        if (body is null)
        {
            return;
        }

        var creation = FindCreation(memberAccess.Expression, body, invocation, memoryStreamType, context);

        if (creation is null)
        {
            return;
        }

        ReportIfNotVisible(context, creation, memoryStreamType, invocation);
    }

    private static BaseObjectCreationExpressionSyntax? FindCreation(
        ExpressionSyntax receiver,
        BlockSyntax body,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol memoryStreamType,
        SyntaxNodeAnalysisContext context)
    {
        var model = context.SemanticModel;
        var token = context.CancellationToken;

        return model.GetSymbolInfo(receiver, token).Symbol switch
        {
            ILocalSymbol local when IsMemoryStream(local.Type, memoryStreamType) =>
                FindLocalCreation(local, body, invocation, model, token),
            IParameterSymbol parameter when IsMemoryStream(parameter.Type, memoryStreamType) =>
                FindParameterCreation(parameter, body, invocation, model, token),
            _ => null
        };
    }

    private static BaseObjectCreationExpressionSyntax? FindLocalCreation(
        ILocalSymbol local,
        BlockSyntax body,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var assignment = FindNearestAssignment(local, body, invocation, model, cancellationToken);
        if (assignment is not null)
        {
            return assignment.Right as BaseObjectCreationExpressionSyntax;
        }

        // A declaration outside the body, such as a field initializer, is invisible to the scan.
        foreach (var reference in local.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is VariableDeclaratorSyntax declarator
                && body.Span.Contains(declarator.Span))
            {
                return declarator.Initializer?.Value as BaseObjectCreationExpressionSyntax;
            }
        }

        return null;
    }

    private static BaseObjectCreationExpressionSyntax? FindParameterCreation(
        IParameterSymbol parameter,
        BlockSyntax body,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return FindNearestAssignment(parameter, body, invocation, model, cancellationToken)?.Right
            as BaseObjectCreationExpressionSyntax;
    }

    private static AssignmentExpressionSyntax? FindNearestAssignment(
        ISymbol symbol,
        BlockSyntax body,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        AssignmentExpressionSyntax? nearest = null;
        var nearestStart = -1;

        foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                || assignment.SpanStart >= invocation.SpanStart)
            {
                continue;
            }

            var assigned = model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol;
            if (assigned is null || !SymbolEqualityComparer.Default.Equals(assigned, symbol))
            {
                continue;
            }

            if (assignment.SpanStart > nearestStart)
            {
                nearestStart = assignment.SpanStart;
                nearest = assignment;
            }
        }

        return nearest;
    }

    private static void ReportIfNotVisible(
        SyntaxNodeAnalysisContext context,
        BaseObjectCreationExpressionSyntax creation,
        INamedTypeSymbol memoryStreamType,
        InvocationExpressionSyntax invocation)
    {
        var model = context.SemanticModel;
        var token = context.CancellationToken;

        if (!IsMemoryStream(model.GetTypeInfo(creation, token).Type, memoryStreamType)
            || model.GetSymbolInfo(creation, token).Symbol is not IMethodSymbol constructor)
        {
            return;
        }

        var visibleArgument = FindPubliclyVisibleArgument(creation, constructor);

        if (visibleArgument is null)
        {
            // Only the byte[] constructors default to a hidden buffer. The empty and capacity
            // constructors expose it, so GetBuffer works on them.
            if (constructor.Parameters.Length > 0
                && constructor.Parameters[0].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            }

            return;
        }

        var constant = model.GetConstantValue(visibleArgument, token);

        // A non-constant argument may be true at run time, so the rule cannot claim the call throws.
        if (!constant.HasValue || constant.Value is not bool isVisible || isVisible)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static ExpressionSyntax? FindPubliclyVisibleArgument(
        BaseObjectCreationExpressionSyntax creation,
        IMethodSymbol constructor)
    {
        if (creation.ArgumentList is not { } argumentList)
        {
            return null;
        }

        var positionalIndex = 0;

        foreach (var argument in argumentList.Arguments)
        {
            string parameterName;

            if (argument.NameColon is { } nameColon)
            {
                parameterName = nameColon.Name.Identifier.ValueText;
            }
            else
            {
                if (positionalIndex >= constructor.Parameters.Length)
                {
                    return null;
                }

                parameterName = constructor.Parameters[positionalIndex].Name;
                positionalIndex++;
            }

            if (string.Equals(parameterName, PubliclyVisibleParameterName, StringComparison.Ordinal))
            {
                return argument.Expression;
            }
        }

        return null;
    }

    private static bool IsMemoryStream(ITypeSymbol? type, INamedTypeSymbol memoryStreamType)
    {
        return type is not null
               && SymbolEqualityComparer.Default.Equals(
                   type.WithNullableAnnotation(NullableAnnotation.NotAnnotated),
                   memoryStreamType);
    }
}
