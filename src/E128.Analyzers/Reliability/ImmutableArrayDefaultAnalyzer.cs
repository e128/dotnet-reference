using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128163: Reports an <c lang="csharp">ImmutableArray&lt;T&gt;</c> field that no initializer
///     and no assignment reaches. The field keeps its default value, and a default array throws
///     <c lang="csharp">NullReferenceException</c> on every operation that reads the underlying
///     array.
/// </summary>
/// <remarks>
///     The rule collects assignments across the whole compilation, so a partial type that assigns
///     the field in another file stays silent. An assignment of any kind counts, including one in a
///     constructor or an initializer block.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImmutableArrayDefaultAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128163";

    private const string ImmutableArrayMetadataName = "System.Collections.Immutable.ImmutableArray`1";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "ImmutableArray field left at its default",
        "'{0}' is never assigned, so it stays at the default ImmutableArray",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "The default ImmutableArray holds a null inner array, and every operation that reads it " +
        "throws NullReferenceException. Initialize the field to ImmutableArray<T>.Empty or assign " +
        "it in the constructor.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

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
        var immutableArrayTypes = MetadataTypes.Find(context.Compilation, ImmutableArrayMetadataName);

        if (immutableArrayTypes.IsEmpty)
        {
            return;
        }

        var candidates = new ConcurrentBag<FieldCandidate>();
        var assignedFields = new ConcurrentBag<IFieldSymbol>();

        context.RegisterSyntaxNodeAction(
            ctx => CollectField(ctx, immutableArrayTypes, candidates),
            SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(
            ctx => CollectAssignment(ctx, assignedFields),
            SyntaxKind.SimpleAssignmentExpression);
        context.RegisterCompilationEndAction(ctx =>
            Report(candidates, assignedFields, ctx.ReportDiagnostic));
    }

    private static void CollectField(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> immutableArrayTypes,
        ConcurrentBag<FieldCandidate> candidates)
    {
        var declaration = (FieldDeclarationSyntax)context.Node;

        foreach (var variable in declaration.Declaration.Variables)
        {
            if (variable.Initializer is not null)
            {
                continue;
            }

            if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)
                    is not IFieldSymbol field
                || field.Type is not INamedTypeSymbol fieldType
                || !MetadataTypes.Contains(immutableArrayTypes, fieldType.OriginalDefinition))
            {
                continue;
            }

            candidates.Add(new FieldCandidate(variable.GetLocation(), field));
        }
    }

    private static void CollectAssignment(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<IFieldSymbol> assignedFields)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
            is IFieldSymbol field)
        {
            assignedFields.Add(field);
        }
    }

    private static void Report(
        ConcurrentBag<FieldCandidate> candidates,
        ConcurrentBag<IFieldSymbol> assignedFields,
        Action<Diagnostic> reportDiagnostic)
    {
        if (candidates.IsEmpty)
        {
            return;
        }

        var assigned = assignedFields.ToImmutableHashSet<IFieldSymbol>(SymbolEqualityComparer.Default);

        foreach (var candidate in candidates)
        {
            if (assigned.Contains(candidate.Field))
            {
                continue;
            }

            reportDiagnostic(Diagnostic.Create(Rule, candidate.Location, candidate.Field.Name));
        }
    }

    private sealed class FieldCandidate
    {
        internal FieldCandidate(Location location, IFieldSymbol field)
        {
            Location = location;
            Field = field;
        }

        internal Location Location { get; }

        internal IFieldSymbol Field { get; }
    }
}
