using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128174: Flags a constructor, or a primary constructor, that takes more reference-type dependencies than the
///     <c lang="csharp">e128_max_ctor_dependencies</c> ceiling. A long dependency list marks a type with too many jobs.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstructorDependencyCeilingAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128174";

    internal const string OptionKey = "e128_max_ctor_dependencies";

    private const int DefaultCeiling = 7;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Reduce constructor dependencies",
        "'{0}' takes {1} dependencies, over the ceiling of {2}: split the type by responsibility",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true,
        "A constructor with many class or interface parameters marks a type that does several jobs. Split it, or group related dependencies behind one service. Set e128_max_ctor_dependencies to change the ceiling.");

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
        var known = new KnownTypes(
            context.Compilation.GetTypeByMetadataName("System.Uri"),
            context.Compilation.GetTypeByMetadataName("System.Exception"),
            context.Compilation.GetSpecialType(SpecialType.System_Collections_IEnumerable));

        // Each tree carries its own analyzer config. Read every ceiling once here instead of on
        // every constructor declaration.
        var ceilingsBuilder = ImmutableArray.CreateBuilder<(SyntaxTree Tree, int Ceiling)>();
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            ceilingsBuilder.Add((tree, ReadCeiling(tree, context.Options.AnalyzerConfigOptionsProvider)));
        }

        var ceilings = ceilingsBuilder.ToImmutable();

        // A record primary constructor is not registered. Its parameters declare data members, not services.
        context.RegisterSyntaxNodeAction(ctx => AnalyzeConstructor(ctx, known, ceilings), SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(ctx => AnalyzePrimaryConstructor(ctx, known, ceilings), SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
    }

    private static void AnalyzeConstructor(
        SyntaxNodeAnalysisContext context,
        KnownTypes known,
        ImmutableArray<(SyntaxTree Tree, int Ceiling)> ceilings)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node; Check(context, constructor.ParameterList, constructor.Identifier, known, ceilings);
    }

    private static void AnalyzePrimaryConstructor(
        SyntaxNodeAnalysisContext context,
        KnownTypes known,
        ImmutableArray<(SyntaxTree Tree, int Ceiling)> ceilings)
    {
        var type = (TypeDeclarationSyntax)context.Node;

        if (type.ParameterList is { } parameters)
        {
            Check(context, parameters, type.Identifier, known, ceilings);
        }
    }

    private static void Check(
        SyntaxNodeAnalysisContext context,
        ParameterListSyntax parameters,
        SyntaxToken identifier,
        KnownTypes known,
        ImmutableArray<(SyntaxTree Tree, int Ceiling)> ceilings)
    {
        var ceiling = DefaultCeiling;
        foreach (var (tree, configured) in ceilings)
        {
            if (ReferenceEquals(tree, context.Node.SyntaxTree))
            {
                ceiling = configured;
                break;
            }
        }

        var count = 0;

        foreach (var parameter in parameters.Parameters)
        {
            if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken)?.Type is { } type
                && IsDependency(type, known))
            {
                count++;
            }
        }

        if (count > ceiling)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), identifier.ValueText, count, ceiling));
        }
    }

    private static bool IsDependency(ITypeSymbol type, KnownTypes known)
    {
        // Special types cover object, string, primitives, and the generic collection interfaces.
        // Arrays, structs, enums, and delegates fail the type kind check, so a params array never counts.
        if (type.SpecialType != SpecialType.None
            || type.TypeKind is not (TypeKind.Class or TypeKind.Interface)
            || type.IsRecord
            || SymbolEqualityComparer.Default.Equals(type, known.Uri))
        {
            return false;
        }

        // An exception or a collection carries data. It does not provide a service.
        return !DerivesFrom(type, known.Exception) && !IsEnumerable(type, known.Enumerable);
    }

    private static bool DerivesFrom(ITypeSymbol type, ITypeSymbol? baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnumerable(ITypeSymbol type, ITypeSymbol enumerable)
    {
        if (SymbolEqualityComparer.Default.Equals(type, enumerable))
        {
            return true;
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(implemented, enumerable))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class KnownTypes
    {
        internal KnownTypes(INamedTypeSymbol? uri, INamedTypeSymbol? exception, INamedTypeSymbol enumerable)
        {
            Uri = uri;
            Exception = exception;
            Enumerable = enumerable;
        }

        internal INamedTypeSymbol? Uri { get; }

        internal INamedTypeSymbol? Exception { get; }

        internal INamedTypeSymbol Enumerable { get; }
    }

    private static int ReadCeiling(SyntaxTree tree, AnalyzerConfigOptionsProvider optionsProvider)
    {
        var options = optionsProvider.GetOptions(tree);

        return options.TryGetValue(OptionKey, out var text)
               && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
               && value > 0
            ? value
            : DefaultCeiling;
    }
}
