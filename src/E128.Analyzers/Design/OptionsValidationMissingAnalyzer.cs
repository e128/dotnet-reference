using System;
using System.Collections.Immutable;
using System.Linq;
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
///     reuses a method name. No code fix.
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

        var builderExtensionTypes = BuilderExtensionMetadataNames
            .SelectMany(metadataName => MetadataTypes.Find(context.Compilation, metadataName))
            .ToImmutableArray();

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeInvocation(
                nodeContext,
                serviceCollectionExtensions,
                serviceCollectionType,
                configurationType,
                optionsBuilderType,
                builderExtensionTypes),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> serviceCollectionExtensions,
        INamedTypeSymbol? serviceCollectionType,
        INamedTypeSymbol? configurationType,
        INamedTypeSymbol? optionsBuilderType,
        ImmutableArray<INamedTypeSymbol> builderExtensionTypes)
    {
        var outermost = (InvocationExpressionSyntax)context.Node;

        // Walk only the outermost call of a fluent chain, so a validation call that follows the
        // registration stays inside the chain the rule reads.
        if (outermost.Parent is MemberAccessExpressionSyntax access && ReferenceEquals(access.Expression, outermost))
        {
            return;
        }

        if (!ChainMentionsRegistration(outermost))
        {
            return;
        }

        InvocationExpressionSyntax? registration = null;
        IMethodSymbol? registrationMethod = null;
        var registrationBindsConfiguration = false;
        var hasBind = false;

        ExpressionSyntax? current = outermost;
        while (current is InvocationExpressionSyntax invocation)
        {
            if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method)
            {
                if (IsValidationCall(method, optionsBuilderType, builderExtensionTypes))
                {
                    return;
                }

                if (MetadataTypes.Contains(serviceCollectionExtensions, method.ContainingType)
                    && IsOptionsRegistration(method, serviceCollectionType, configurationType, out var bindsConfiguration))
                {
                    // The outermost registration wins, so its bind flag must win too.
                    registrationBindsConfiguration = registration is null ? bindsConfiguration : registrationBindsConfiguration;
                    registration ??= invocation;
                    registrationMethod ??= method;
                }
                else if (IsBindCall(method, builderExtensionTypes))
                {
                    hasBind = true;
                }
            }

            current = invocation.Expression is MemberAccessExpressionSyntax member ? member.Expression : null;
        }

        // A bare AddOptions<T>() carries no configuration source here, so it needs a Bind call.
        if (registration is null
            || registrationMethod is null
            || registrationMethod.TypeArguments.Length != 1
            || (!hasBind && !registrationBindsConfiguration))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            registration.GetLocation(),
            registrationMethod.TypeArguments[0].Name));
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

        // Configure<T>(configuration) binds a configuration source. Configure<T>(Action<T>) does not.
        bindsConfiguration = method.Parameters.Length > 0
            && SymbolEqualityComparer.Default.Equals(
                method.Parameters[method.Parameters.Length - 1].Type,
                configurationType);

        return bindsConfiguration;
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
