using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Design;

/// <summary>
///     E128257: A property on a configuration-bound options type is not public. The
///     configuration binder and DataAnnotations read public properties only, so the
///     property keeps its default value and ignores the configuration key.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OptionsPropertyAccessibilityAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128257";

    private const string OptionsBuilderMetadataName = "Microsoft.Extensions.Options.OptionsBuilder`1";

    private const string ConfigureExtensionsMetadataName =
        "Microsoft.Extensions.DependencyInjection.OptionsConfigurationServiceCollectionExtensions";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Bound options property is not public",
        "Property '{0}' on bound options type '{1}' is not public: the binder and DataAnnotations read public properties only",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true,
        "The configuration binder and DataAnnotations read public instance properties only. A non-public property keeps its default value and ignores the configuration key. Make the property public, or remove it from the options type.");

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
            var optionsBuilder = start.Compilation.GetTypeByMetadataName(OptionsBuilderMetadataName);
            var configureExtensions = start.Compilation.GetTypeByMetadataName(ConfigureExtensionsMetadataName);

            if (optionsBuilder is null && configureExtensions is null)
            {
                return;
            }

            var reported = new ConcurrentDictionary<ISymbol, bool>(SymbolEqualityComparer.Default);

            start.RegisterOperationAction(
                operation => Analyze(operation, optionsBuilder, configureExtensions, reported),
                OperationKind.Invocation);
        });
    }

    private static void Analyze(
        OperationAnalysisContext context,
        INamedTypeSymbol? optionsBuilder,
        INamedTypeSymbol? configureExtensions,
        ConcurrentDictionary<ISymbol, bool> reported)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var optionsType = ResolveOptionsType(invocation, optionsBuilder, configureExtensions);

        if (optionsType is null || !reported.TryAdd(optionsType, true))
        {
            return;
        }

        foreach (var property in optionsType.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsStatic
                || property.IsIndexer
                || property.SetMethod is not { } setter
                || setter.DeclaredAccessibility is Accessibility.Public)
            {
                continue;
            }

            var location = property.Locations.FirstOrDefault(static candidate => candidate.IsInSource);

            if (location is null)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, property.Name, optionsType.Name));
        }
    }

    private static INamedTypeSymbol? ResolveOptionsType(
        IInvocationOperation invocation,
        INamedTypeSymbol? optionsBuilder,
        INamedTypeSymbol? configureExtensions)
    {
        var method = invocation.TargetMethod;

        if (IsBind(method.Name))
        {
            return ResolveFromBuilder(invocation, optionsBuilder);
        }

        return IsConfigure(method, configureExtensions)
            ? method.TypeArguments[0] as INamedTypeSymbol
            : null;
    }

    private static bool IsBind(string name)
    {
        return string.Equals(name, "Bind", StringComparison.Ordinal)
               || string.Equals(name, "BindConfiguration", StringComparison.Ordinal);
    }

    private static INamedTypeSymbol? ResolveFromBuilder(
        IInvocationOperation invocation,
        INamedTypeSymbol? optionsBuilder)
    {
        var receiver = invocation.TargetMethod.IsExtensionMethod && invocation.Arguments.Length > 0
            ? invocation.Arguments[0].Value
            : invocation.Instance;

        if (receiver?.Type is not INamedTypeSymbol builderType
            || optionsBuilder is null
            || !SymbolEqualityComparer.Default.Equals(builderType.OriginalDefinition, optionsBuilder)
            || builderType.TypeArguments.Length is not 1)
        {
            return null;
        }

        return builderType.TypeArguments[0] as INamedTypeSymbol;
    }

    private static bool IsConfigure(IMethodSymbol method, INamedTypeSymbol? configureExtensions)
    {
        return string.Equals(method.Name, "Configure", StringComparison.Ordinal)
               && configureExtensions is not null
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, configureExtensions)
               && method.TypeArguments.Length is 1;
    }
}
