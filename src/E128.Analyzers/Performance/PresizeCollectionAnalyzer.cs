using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PresizeCollectionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128205";
    internal const string CapacityProperty = "CapacityExpression";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Collection filled in a loop has a known final count",
        "'{0}' is filled in a loop with a known count — pass the count to the constructor",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

    // Only types that expose a public (int capacity) constructor belong here. SortedSet
    // and SortedDictionary have no capacity constructor, so the fix cannot apply to them.
    private static readonly ImmutableArray<(string MetadataName, string FillMethod)> TargetTypes =
    [
        ("System.Collections.Generic.List`1", "Add"),
        ("System.Collections.Generic.Dictionary`2", "Add"),
        ("System.Collections.Generic.HashSet`1", "Add"),
        ("System.Collections.Generic.Queue`1", "Enqueue"),
        ("System.Collections.Generic.Stack`1", "Push")
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var targets = ResolveTargets(startContext.Compilation);
            if (targets.IsEmpty)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeDeclaration(nodeContext, targets),
                SyntaxKind.LocalDeclarationStatement);
        });
    }

    private static ImmutableArray<(INamedTypeSymbol Type, string FillMethod)> ResolveTargets(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<(INamedTypeSymbol Type, string FillMethod)>();
        foreach (var (metadataName, fillMethod) in TargetTypes)
        {
            var type = compilation.GetTypeByMetadataName(metadataName);
            if (type is not null && HasCapacityConstructor(type))
            {
                builder.Add((type, fillMethod));
            }
        }

        return builder.ToImmutable();
    }

    private static bool HasCapacityConstructor(INamedTypeSymbol type)
    {
        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.Parameters.Length == 1
                && constructor.Parameters[0].Type.SpecialType == SpecialType.System_Int32
                && constructor.DeclaredAccessibility == Accessibility.Public)
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeDeclaration(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<(INamedTypeSymbol Type, string FillMethod)> targets)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (!TryGetCreation(declaration, context, targets, out var variable, out var creation, out var fillMethod))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol local)
        {
            return;
        }

        var loop = LoopCount.TryGetNextFillLoop(
            declaration,
            local,
            [fillMethod],
            context.SemanticModel,
            context.CancellationToken);
        if (loop is null)
        {
            return;
        }

        var countExpression = LoopCount.TryGetCount(loop, context.SemanticModel, context.CancellationToken);
        if (countExpression is null)
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(CapacityProperty, countExpression);
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            creation.GetLocation(),
            properties,
            variable.Identifier.ValueText));
    }

    private static bool TryGetCreation(
        LocalDeclarationStatementSyntax declaration,
        SyntaxNodeAnalysisContext context,
        ImmutableArray<(INamedTypeSymbol Type, string FillMethod)> targets,
        [NotNullWhen(true)] out VariableDeclaratorSyntax? variable,
        [NotNullWhen(true)] out BaseObjectCreationExpressionSyntax? creation,
        [NotNullWhen(true)] out string? fillMethod)
    {
        variable = null;
        creation = null;
        fillMethod = null;

        if (declaration.Declaration.Variables.Count != 1)
        {
            return false;
        }

        var candidate = declaration.Declaration.Variables[0];
        if (candidate.Initializer?.Value is not BaseObjectCreationExpressionSyntax candidateCreation
            || candidateCreation.ArgumentList is { Arguments.Count: > 0 }
            || InitializerAssignments.Assigns(candidateCreation, "Capacity"))
        {
            return false;
        }

        var createdType = context.SemanticModel.GetTypeInfo(candidateCreation, context.CancellationToken).Type;
        if (createdType is null || !TryGetFillMethod(createdType, targets, out fillMethod))
        {
            return false;
        }

        variable = candidate;
        creation = candidateCreation;
        return true;
    }

    private static bool TryGetFillMethod(
        ITypeSymbol type,
        ImmutableArray<(INamedTypeSymbol Type, string FillMethod)> targets,
        [NotNullWhen(true)] out string? fillMethod)
    {
        fillMethod = null;
        foreach (var (target, method) in targets)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, target))
            {
                fillMethod = method;
                return true;
            }
        }

        return false;
    }
}
