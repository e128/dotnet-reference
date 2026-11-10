using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128161: Reports a <c lang="csharp">Path.Combine</c> call whose constant segment is rooted
///     or climbs to a parent directory. A rooted segment discards every earlier segment, so the
///     result escapes the intended base directory.
/// </summary>
/// <remarks>
///     The rule reads constant segments only. A segment computed at run time carries no decision
///     here. A bare <c lang="csharp">".."</c> segment stays silent, because a caller uses it
///     deliberately to step one level up.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PathCombineSegmentAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128161";

    internal const string ReplacementMethodName = "Path.Join";

    private const string RootedAdvice = "a rooted segment drops every earlier segment, so use Path.Join";

    private const string ParentAdvice =
        "call Path.GetFullPath on the result and check that it starts with the base directory";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Path.Combine with a rooted or parent segment",
        "'{0}' can leave the base directory: {1}",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Path.Combine returns a rooted segment on its own and drops every earlier segment. Use " +
        "Path.Join for a rooted segment, which keeps every segment. A segment that climbs with a " +
        "parent reference can leave the base directory, and Path.Join does not stop that. Call " +
        "Path.GetFullPath on the result and check that it starts with the base directory.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(memberAccess.Name.Identifier.ValueText, "Combine", StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count < 2)
        {
            return;
        }

        if (!IsSystemPathCombine(invocation, context.SemanticModel, context))
        {
            return;
        }

        // The first argument is the base, and a rooted base discards nothing, so the scan starts at
        // the second segment.
        for (var index = 1; index < invocation.ArgumentList.Arguments.Count; index++)
        {
            var argument = invocation.ArgumentList.Arguments[index];
            var constant = context.SemanticModel.GetConstantValue(
                argument.Expression,
                context.CancellationToken);

            if (!constant.HasValue || constant.Value is not string segment)
            {
                continue;
            }

            if (IsRooted(segment))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, argument.GetLocation(), segment, RootedAdvice));
            }
            else if (EscapesBase(segment))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, argument.GetLocation(), segment, ParentAdvice));
            }
        }
    }

    private static bool IsSystemPathCombine(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        SyntaxNodeAnalysisContext context)
    {
        return model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
               && string.Equals(method.Name, "Combine", StringComparison.Ordinal)
               && string.Equals(
                   method.ContainingType?.ToDisplayString(),
                   "System.IO.Path",
                   StringComparison.Ordinal);
    }

    /// <summary>
    ///     True when the segment starts from a root, so <c lang="csharp">Path.Combine</c> keeps that
    ///     segment only. The test reads both separators and the drive-letter form, because the
    ///     analyzer runs on one platform and the source targets another.
    /// </summary>
    internal static bool IsRooted(string segment)
    {
        return segment.Length > 0
               && (segment[0] is '/' or '\\'
                   || (segment.Length > 1 && segment[1] == ':' && IsAsciiLetter(segment[0])));
    }

    /// <summary>
    ///     True when a multi-segment constant starts by climbing to the parent directory, so the
    ///     result lands outside the base. The walk counts depth: a name adds one, a
    ///     <c lang="csharp">".."</c> removes one, and a depth below zero escapes. A segment that is
    ///     exactly <c lang="csharp">".."</c> is a deliberate step up, and
    ///     <c lang="csharp">"a/../b"</c> stays inside the base, so both stay silent.
    ///     <c lang="csharp">"a/../../b"</c> escapes.
    /// </summary>
    internal static bool EscapesBase(string segment)
    {
        var segments = segment.Split('/', '\\');

        if (segments.Length < 2)
        {
            return false;
        }

        var depth = 0;

        foreach (var part in segments)
        {
            if (part.Length is 0 || string.Equals(part, ".", StringComparison.Ordinal))
            {
                continue;
            }

            depth += string.Equals(part, "..", StringComparison.Ordinal) ? -1 : 1;

            if (depth < 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
    }
}
