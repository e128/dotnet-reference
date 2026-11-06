using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128179: Flags an options registration that binds configuration with no validation call in the same
///     statement chain. A registration with no validation accepts a missing or malformed configuration value,
///     and the application fails later, at the point of use.
/// </summary>
/// <remarks>
///     The rule covers <c lang="csharp">services.AddOptions&lt;T&gt;().Bind(...)</c>,
///     <c lang="csharp">services.AddOptions&lt;T&gt;().BindConfiguration(...)</c>, and
///     <c lang="csharp">services.Configure&lt;T&gt;(configuration)</c>. It reports the registration call
///     (<c lang="csharp">AddOptions</c> or <c lang="csharp">Configure</c>). A bare
///     <c lang="csharp">services.AddOptions&lt;T&gt;()</c> carries no configuration source, and
///     <c lang="csharp">PostConfigure</c> is a different shape, so the rule skips both. The rule matches the
///     receiver chain through the framework extension method symbols, so it stays silent on a user type that
///     reuses a method name. A separate <c lang="csharp">IValidateOptions&lt;T&gt;</c> registration for the
///     same options type in the same member or file also clears the report, because that validator runs for
///     the bound options. No code fix.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OptionsValidationMissingAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128179";

    private const string ServiceCollectionExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.OptionsServiceCollectionExtensions";

    // Configure<T>(IConfiguration) lives in Microsoft.Extensions.Options.ConfigurationExtensions, on its
    // own extension class, apart from AddOptions<T> and Configure<T>(Action<T>).
    private const string ConfigurationExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.OptionsConfigurationServiceCollectionExtensions";
    private const string ServiceCollectionMetadataName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";
    private const string ConfigurationMetadataName = "Microsoft.Extensions.Configuration.IConfiguration";
    private const string OptionsBuilderMetadataName = "Microsoft.Extensions.Options.OptionsBuilder`1";
    private const string ValidateOptionsMetadataName = "Microsoft.Extensions.Options.IValidateOptions`1";

    // The validation methods live on the options builder and on four extension classes, and the
    // owning assembly differs by method. Resolve every candidate, so the name alone never decides.
    private static readonly ImmutableArray<string> BuilderExtensionMetadataNames =
    [
        "Microsoft.Extensions.DependencyInjection.OptionsBuilderConfigurationExtensions",
        "Microsoft.Extensions.DependencyInjection.OptionsBuilderExtensions",
        "Microsoft.Extensions.DependencyInjection.OptionsBuilderDataAnnotationsExtensions",
        "Microsoft.Extensions.Options.OptionsBuilderExtensions"
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Options registration binds configuration with no validation",
        "Options type '{0}' is bound from configuration with no validation call",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true,
        "An options registration that binds configuration with no Validate, ValidateDataAnnotations, " +
        "ValidateOnStart, ValidateEagerly, or ValidateAllProperties call accepts a missing or malformed " +
        "configuration value. The application then fails later, at the point of use. Add a validation call " +
        "to the same registration chain.");

    private static readonly ImmutableHashSet<string> RegistrationMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "AddOptions",
        "Configure");

    private static readonly ImmutableHashSet<string> BindMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Bind",
        "BindConfiguration");

    private static readonly ImmutableHashSet<string> ValidationMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Validate",
        "ValidateDataAnnotations",
        "ValidateOnStart",
        "ValidateEagerly",
        "ValidateAllProperties");

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
        // Without the framework registration type the rule has no symbol to match, so it stays off
        // rather than falling back to a method name.
        var serviceCollectionExtensions = MetadataTypes.Find(context.Compilation, ServiceCollectionExtensionsMetadataName)
            .AddRange(MetadataTypes.Find(context.Compilation, ConfigurationExtensionsMetadataName));
        if (serviceCollectionExtensions.IsEmpty)
        {
            return;
        }

        var serviceCollectionType = context.Compilation.GetTypeByMetadataName(ServiceCollectionMetadataName);
        var configurationType = context.Compilation.GetTypeByMetadataName(ConfigurationMetadataName);
        var optionsBuilderType = context.Compilation.GetTypeByMetadataName(OptionsBuilderMetadataName);
        var validateOptionsType = context.Compilation.GetTypeByMetadataName(ValidateOptionsMetadataName);

        var builderExtensionTypes = BuilderExtensionMetadataNames
            .SelectMany(metadataName => MetadataTypes.Find(context.Compilation, metadataName))
            .ToImmutableArray();

        var types = new RuleTypes(
            serviceCollectionExtensions,
            serviceCollectionType,
            configurationType,
            optionsBuilderType,
            validateOptionsType,
            builderExtensionTypes);

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeInvocation(nodeContext, types),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, RuleTypes types)
    {
        var outermost = (InvocationExpressionSyntax)context.Node;

        // Walk only the outermost call of a fluent chain, so a validation call that follows the
        // registration stays inside the chain the rule reads.
        if (outermost.Parent is MemberAccessExpressionSyntax access && ReferenceEquals(access.Expression, outermost))
        {
            return;
        }

        if (!ChainMentionsRegistration(outermost)
            || !TryFindRegistration(
                outermost,
                types,
                context.SemanticModel,
                context.CancellationToken,
                out var registration,
                out var registrationMethod))
        {
            return;
        }

        // A validator registered elsewhere for the same options type runs for the bound options.
        if (HasSeparateValidator(
                registration,
                registrationMethod.TypeArguments[0],
                types.ValidateOptions,
                types.ServiceCollection,
                context.SemanticModel,
                context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            registration.GetLocation(),
            registrationMethod.TypeArguments[0].Name));
    }

    /// <summary>
    ///     Finds the options registration in the chain and checks that it reads a configuration
    ///     source. A validation call in the chain clears the report.
    /// </summary>
    private static bool TryFindRegistration(
        InvocationExpressionSyntax outermost,
        RuleTypes types,
        SemanticModel model,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out InvocationExpressionSyntax? registration,
        [NotNullWhen(true)] out IMethodSymbol? registrationMethod)
    {
        registration = null;
        registrationMethod = null;

        var registrationBindsConfiguration = false;
        var hasBind = false;

        ExpressionSyntax? current = outermost;
        while (current is InvocationExpressionSyntax invocation)
        {
            if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method)
            {
                if (IsValidationCall(method, types.OptionsBuilder, types.BuilderExtensions))
                {
                    return false;
                }

                if (MetadataTypes.Contains(types.ServiceCollectionExtensions, method.ContainingType)
                    && IsOptionsRegistration(method, types.ServiceCollection, types.Configuration, out var bindsConfiguration))
                {
                    // The outermost registration wins, so its bind flag must win too.
                    registrationBindsConfiguration = registration is null ? bindsConfiguration : registrationBindsConfiguration;
                    registration ??= invocation;
                    registrationMethod ??= method;
                }
                else if (IsBindCall(method, types.BuilderExtensions))
                {
                    hasBind = true;
                }
            }

            current = invocation.Expression is MemberAccessExpressionSyntax member ? member.Expression : null;
        }

        // A bare AddOptions<T>() carries no configuration source here, so it needs a Bind call.
        return registration is not null
               && registrationMethod is not null
               && registrationMethod.TypeArguments.Length == 1
               && (hasBind || registrationBindsConfiguration);
    }

    private readonly struct RuleTypes
    {
        internal RuleTypes(
            ImmutableArray<INamedTypeSymbol> serviceCollectionExtensions,
            INamedTypeSymbol? serviceCollection,
            INamedTypeSymbol? configuration,
            INamedTypeSymbol? optionsBuilder,
            INamedTypeSymbol? validateOptions,
            ImmutableArray<INamedTypeSymbol> builderExtensions)
        {
            ServiceCollectionExtensions = serviceCollectionExtensions;
            ServiceCollection = serviceCollection;
            Configuration = configuration;
            OptionsBuilder = optionsBuilder;
            ValidateOptions = validateOptions;
            BuilderExtensions = builderExtensions;
        }

        internal ImmutableArray<INamedTypeSymbol> ServiceCollectionExtensions { get; }

        internal INamedTypeSymbol? ServiceCollection { get; }

        internal INamedTypeSymbol? Configuration { get; }

        internal INamedTypeSymbol? OptionsBuilder { get; }

        internal INamedTypeSymbol? ValidateOptions { get; }

        internal ImmutableArray<INamedTypeSymbol> BuilderExtensions { get; }
    }

    /// <summary>
    ///     True when the same member, or failing that the same file, registers an
    ///     <c lang="csharp">IValidateOptions&lt;T&gt;</c> implementation for the options type.
    /// </summary>
    private static bool HasSeparateValidator(
        InvocationExpressionSyntax registration,
        ITypeSymbol optionsType,
        INamedTypeSymbol? validateOptionsType,
        INamedTypeSymbol? serviceCollectionType,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (validateOptionsType is null || serviceCollectionType is null)
        {
            return false;
        }

        var member = registration.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        if (member is not null
            && ContainsValidatorRegistration(
                member,
                optionsType,
                validateOptionsType,
                serviceCollectionType,
                model,
                cancellationToken))
        {
            return true;
        }

        var root = registration.SyntaxTree.GetRoot(cancellationToken);

        return !ReferenceEquals(root, member)
               && ContainsValidatorRegistration(
                   root,
                   optionsType,
                   validateOptionsType,
                   serviceCollectionType,
                   model,
                   cancellationToken);
    }

    private static bool ContainsValidatorRegistration(
        SyntaxNode scope,
        ITypeSymbol optionsType,
        INamedTypeSymbol validateOptionsType,
        INamedTypeSymbol serviceCollectionType,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in scope.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetTypeInfo(access.Expression, cancellationToken).Type,
                    serviceCollectionType)
                || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
            {
                continue;
            }

            foreach (var typeArgument in method.TypeArguments)
            {
                if (typeArgument is INamedTypeSymbol { TypeArguments.Length: 1 } named
                    && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, validateOptionsType)
                    && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], optionsType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ChainMentionsRegistration(InvocationExpressionSyntax outermost)
    {
        ExpressionSyntax? current = outermost;
        while (current is InvocationExpressionSyntax invocation)
        {
            var name = invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => null
            };

            if (name is not null && RegistrationMethodNames.Contains(name))
            {
                return true;
            }

            current = invocation.Expression is MemberAccessExpressionSyntax memberAccess
                ? memberAccess.Expression
                : null;
        }

        return false;
    }

    private static bool IsOptionsRegistration(
        IMethodSymbol method,
        INamedTypeSymbol? serviceCollectionType,
        INamedTypeSymbol? configurationType,
        out bool bindsConfiguration)
    {
        bindsConfiguration = false;

        if (string.Equals(method.Name, "AddOptions", StringComparison.Ordinal))
        {
            // The parameterless form is the registration that a Bind call follows. The delegate form
            // configures values in code and is outside this rule.
            return method.Parameters.Length == 0
                || (method.Parameters.Length == 1
                    && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, serviceCollectionType));
        }

        if (!string.Equals(method.Name, "Configure", StringComparison.Ordinal))
        {
            return false;
        }

        // Configure<T>(configuration, ...) binds a configuration source, and the source is not always
        // the last parameter. Configure<T>(Action<T>) does not bind configuration.
        bindsConfiguration = HasConfigurationParameter(method, configurationType);

        return bindsConfiguration;
    }

    private static bool HasConfigurationParameter(IMethodSymbol method, INamedTypeSymbol? configurationType)
    {
        if (configurationType is null)
        {
            return false;
        }

        foreach (var parameter in method.Parameters)
        {
            if (SymbolEqualityComparer.Default.Equals(parameter.Type, configurationType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBindCall(IMethodSymbol method, ImmutableArray<INamedTypeSymbol> builderExtensionTypes)
    {
        return BindMethodNames.Contains(method.Name)
            && MetadataTypes.Contains(builderExtensionTypes, method.ContainingType);
    }

    private static bool IsValidationCall(
        IMethodSymbol method,
        INamedTypeSymbol? optionsBuilderType,
        ImmutableArray<INamedTypeSymbol> builderExtensionTypes)
    {
        if (!ValidationMethodNames.Contains(method.Name))
        {
            return false;
        }

        // Validate is an instance method on OptionsBuilder<T>. The other methods are extensions.
        return (optionsBuilderType is not null
                && SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, optionsBuilderType))
            || MetadataTypes.Contains(builderExtensionTypes, method.ContainingType);
    }
}
