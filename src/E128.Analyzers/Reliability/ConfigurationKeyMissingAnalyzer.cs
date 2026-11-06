using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128195: Reports a configuration key read through <c lang="csharp">IConfiguration</c> when no
///     JSON additional file defines the key. The rule reads the <c lang="csharp">appsettings*.json</c>
///     files that a consumer passes through <c lang="csharp">AdditionalFiles</c>. Without at least one
///     parsable JSON file the rule stays off.
/// </summary>
/// <remarks>
///     A consumer contract is required. Add
///     <c lang="csharp">&lt;AdditionalFiles Include="appsettings.json" /&gt;</c> to the project file. A
///     key from an environment variable, a user secret, or a file outside the project is not visible
///     to the rule. Add such a key to the JSON file, or set the option
///     <c lang="csharp">e128_added_configuration_keys</c> to a comma-separated list of keys that the
///     rule must accept.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigurationKeyMissingAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128195";

    private const string AddedKeysOptionKey = "e128_added_configuration_keys";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Configuration key absent from the appsettings files",
        "'{0}' is read from configuration but no appsettings file defines the key",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A configuration key read through IConfiguration must exist in an appsettings*.json file that " +
        "the project passes through AdditionalFiles. A missing key returns null or throws at startup.");

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
        var configurationType = context.Compilation.GetTypeByMetadataName(
            "Microsoft.Extensions.Configuration.IConfiguration");
        if (configurationType is null)
        {
            return;
        }

        var configurationExtensions = context.Compilation.GetTypeByMetadataName(
            "Microsoft.Extensions.Configuration.ConfigurationExtensions");

        // A section read uses keys relative to the section, so the rule leaves those reads alone.
        var sectionType = context.Compilation.GetTypeByMetadataName(
            "Microsoft.Extensions.Configuration.IConfigurationSection");

        var keys = ConfigurationKeys.TryCreate(
            context.Options.AdditionalFiles,
            context.Options.AnalyzerConfigOptionsProvider.GlobalOptions,
            AddedKeysOptionKey,
            context.CancellationToken);
        if (keys is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeElementAccess(nodeContext, configurationType, sectionType, keys),
            SyntaxKind.ElementAccessExpression);

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeInvocation(
                nodeContext,
                configurationType,
                configurationExtensions,
                sectionType,
                keys),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeElementAccess(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol configurationType,
        INamedTypeSymbol? sectionType,
        ConfigurationKeys keys)
    {
        var elementAccess = (ElementAccessExpressionSyntax)context.Node;
        if (elementAccess.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        // A write such as config["Key"] = value defines the key, so it is not a read.
        if (elementAccess.Parent is AssignmentExpressionSyntax assignment
            && ReferenceEquals(assignment.Left, elementAccess))
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(
            elementAccess.Expression,
            context.CancellationToken).Type;
        if (!IsOrImplements(receiverType, configurationType)
            || IsOrImplements(receiverType, sectionType))
        {
            return;
        }

        var argument = elementAccess.ArgumentList.Arguments[0].Expression;
        var key = ReadConstantKey(context, argument);
        if (key is null)
        {
            return;
        }

        Report(context, keys, key, elementAccess.GetLocation());
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol configurationType,
        INamedTypeSymbol? configurationExtensions,
        INamedTypeSymbol? sectionType,
        ConfigurationKeys keys)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        // A cheap name check runs before the semantic lookup, which costs more on every call.
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !IsConfigurationMethodName(access.Name.Identifier.ValueText))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
        {
            return;
        }

        if (string.Equals(method.Name, "GetConnectionString", StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, configurationExtensions))
        {
            var name = ReadConstantKey(context, invocation.ArgumentList.Arguments[0].Expression);
            if (name is null)
            {
                return;
            }

            Report(context, keys, "ConnectionStrings:" + name, invocation.GetLocation());
            return;
        }

        // GetSection is an interface member. GetRequiredSection is an extension method.
        var isSectionCall =
            (string.Equals(method.Name, "GetSection", StringComparison.Ordinal)
             && SymbolEqualityComparer.Default.Equals(method.ContainingType, configurationType))
            || (string.Equals(method.Name, "GetRequiredSection", StringComparison.Ordinal)
                && SymbolEqualityComparer.Default.Equals(method.ContainingType, configurationExtensions));
        if (!isSectionCall)
        {
            return;
        }

        // A call on a section reads a key that is relative to that section.
        if (IsOrImplements(
            context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type,
            sectionType))
        {
            return;
        }

        var section = ReadConstantKey(context, invocation.ArgumentList.Arguments[0].Expression);
        if (section is null)
        {
            return;
        }

        Report(context, keys, section, invocation.GetLocation());
    }

    private static bool IsConfigurationMethodName(string name)
    {
        return string.Equals(name, "GetSection", StringComparison.Ordinal)
               || string.Equals(name, "GetRequiredSection", StringComparison.Ordinal)
               || string.Equals(name, "GetConnectionString", StringComparison.Ordinal);
    }

    private static bool IsOrImplements(ITypeSymbol? type, INamedTypeSymbol? target)
    {
        return type is not null
               && target is not null
               && (SymbolEqualityComparer.Default.Equals(type, target)
                   || type.AllInterfaces.Any(
                       implemented => SymbolEqualityComparer.Default.Equals(implemented, target)));
    }

    private static string? ReadConstantKey(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        var constant = context.SemanticModel.GetConstantValue(
            expression,
            context.CancellationToken);
        return !constant.HasValue || constant.Value is not string key || key.Length == 0
            ? null
            : key;
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        ConfigurationKeys keys,
        string key,
        Location location)
    {
        if (keys.Contains(key))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, key));
    }

    private sealed class ConfigurationKeys
    {
        private readonly ImmutableHashSet<string> keys;

        private ConfigurationKeys(ImmutableHashSet<string> keys)
        {
            this.keys = keys;
        }

        internal static ConfigurationKeys? TryCreate(
            ImmutableArray<AdditionalText> files,
            AnalyzerConfigOptions options,
            string addedKeysOptionKey,
            System.Threading.CancellationToken cancellationToken)
        {
            var builder = ImmutableHashSet.CreateBuilder(
                StringComparer.OrdinalIgnoreCase);
            var parsedAny = false;

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file.Path);
                if (!fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
                    || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = file.GetText(cancellationToken);
                if (text is null)
                {
                    continue;
                }

                if (!AppSettingsKeyReader.TryRead(text.ToString(), builder))
                {
                    continue;
                }

                parsedAny = true;
            }

            // The option adds keys on top of a parsed file. It never activates the rule on its own,
            // because without a file the rule cannot tell a missing key from an invisible source.
            if (!parsedAny)
            {
                return null;
            }

            if (options.TryGetValue(addedKeysOptionKey, out var addedKeys)
                && addedKeys is not null)
            {
                foreach (var key in addedKeys.Split(','))
                {
                    var trimmed = key.Trim();
                    if (trimmed.Length > 0)
                    {
                        builder.Add(trimmed);
                    }
                }
            }

            return new ConfigurationKeys(builder.ToImmutable());
        }

        internal bool Contains(string key)
        {
            return keys.Contains(key);
        }
    }
}
