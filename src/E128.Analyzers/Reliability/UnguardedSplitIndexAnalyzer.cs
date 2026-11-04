using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128170: Flags a constant index of 1 or more taken from the result of <c lang="csharp">string.Split</c> with no
///     length check. Split returns one element when the separator is absent, so the index throws on unexpected input.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnguardedSplitIndexAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128170";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Guard the index of a Split result",
        "Index {0} of a Split result has no length check: Split returns one element when the separator is absent",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "string.Split returns a single element when the separator does not occur, so a constant index of 1 or more throws IndexOutOfRangeException on unexpected input. Check Length first, or use a parser that reports failure.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeElementReference, OperationKind.ArrayElementReference);
    }

    private static void AnalyzeElementReference(OperationAnalysisContext context)
    {
        var reference = (IArrayElementReferenceOperation)context.Operation;

        if (reference.Indices.Length != 1
            || reference.Indices[0].ConstantValue is not { HasValue: true, Value: int index }
            || index < 1
            || reference.Syntax is not ElementAccessExpressionSyntax access
            || access.ArgumentList.Arguments[0].Expression is PrefixUnaryExpressionSyntax)
        {
            return;
        }

        if (!IsGuarded(reference.ArrayReference, access, context))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, access.GetLocation(), index));
        }
    }

    private static bool IsGuarded(IOperation array, SyntaxNode access, OperationAnalysisContext context)
    {
        switch (array)
        {
            case IInvocationOperation invocation:
                return !IsStringSplit(invocation);

            case ILocalReferenceOperation local:
                return !IsInitializedFromSplit(local.Local, context)
                       || LengthIsRead(local.Local, access, context);

            default:
                return true;
        }
    }

    private static bool IsStringSplit(IInvocationOperation invocation)
    {
        return string.Equals(invocation.TargetMethod.Name, "Split", StringComparison.Ordinal)
               && invocation.TargetMethod.ContainingType.SpecialType == SpecialType.System_String;
    }

    private static bool IsInitializedFromSplit(ILocalSymbol local, OperationAnalysisContext context)
    {
        foreach (var reference in local.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(context.CancellationToken) is VariableDeclaratorSyntax { Initializer.Value: { } value }
                && context.Operation.SemanticModel?.GetOperation(value, context.CancellationToken) is IInvocationOperation invocation
                && IsStringSplit(invocation))
            {
                return true;
            }
        }

        return false;
    }

    // A read of the local's Length anywhere in the enclosing member counts as a guard. The rule
    // does not model control flow, so it prefers silence over a false report.
    private static bool LengthIsRead(ILocalSymbol local, SyntaxNode access, OperationAnalysisContext context)
    {
        var member = access.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        var model = context.Operation.SemanticModel;

        return member is null
               || model is null
               || member.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess => string.Equals(memberAccess.Name.Identifier.ValueText, "Length", StringComparison.Ordinal)
                                 && SymbolEqualityComparer.Default.Equals(
                                     model.GetSymbolInfo(memberAccess.Expression, context.CancellationToken).Symbol,
                                     local));
    }
}
