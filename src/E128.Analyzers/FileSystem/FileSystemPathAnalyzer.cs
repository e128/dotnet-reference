using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.FileSystem;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileSystemPathAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128001";

    internal const string SuggestedTypeKey = "SuggestedType";
    internal const string SuggestedFileInfo = "FileInfo";
    internal const string SuggestedDirectoryInfo = "DirectoryInfo";
    internal const string SuggestedAmbiguous = "Ambiguous";

    private static readonly ImmutableDictionary<string, string?> FileInfoProperties =
        ImmutableDictionary<string, string?>.Empty.Add(SuggestedTypeKey, SuggestedFileInfo);

    private static readonly ImmutableDictionary<string, string?> DirectoryInfoProperties =
        ImmutableDictionary<string, string?>.Empty.Add(SuggestedTypeKey, SuggestedDirectoryInfo);

    private static readonly ImmutableDictionary<string, string?> AmbiguousProperties =
        ImmutableDictionary<string, string?>.Empty.Add(SuggestedTypeKey, SuggestedAmbiguous);

    // messageFormat placeholders: {0}=paramName, {1}=description, {2}=suggestedType.
    // For the ambiguous (name-only) case, {2} is "FileInfo' or 'DirectoryInfo" — the surrounding
    // single-quotes in the format string produce: Consider using 'FileInfo' or 'DirectoryInfo' instead of 'string'.
    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use FileInfo or DirectoryInfo instead of string for file system paths",
        "Parameter '{0}' appears to represent a {1}. Consider using '{2}' instead of 'string'.",
        "Design",
        DiagnosticSeverity.Warning,
        true);

    // messageFormat: {0}=optionName (e.g. "--input"), {1}=description, {2}=full suggestion (e.g. "'Option<DirectoryInfo>'").
    private static readonly DiagnosticDescriptor OptionRule = new(
        DiagnosticId,
        "Use Option<FileInfo> or Option<DirectoryInfo> instead of Option<string> for file system path options",
        "Option '{0}' appears to represent a {1}. Consider using {2} instead of 'Option<string>'.",
        "Design",
        DiagnosticSeverity.Warning,
        true);

    // messageFormat: {0}=argumentName (e.g. "path"), {1}=description, {2}=full suggestion (e.g. "'Argument<DirectoryInfo>'").
    private static readonly DiagnosticDescriptor ArgumentRule = new(
        DiagnosticId,
        "Use Argument<FileInfo> or Argument<DirectoryInfo> instead of Argument<string> for file system path arguments",
        "Argument '{0}' appears to represent a {1}. Consider using {2} instead of 'Argument<string>'.",
        "Design",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, OptionRule, ArgumentRule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.RecordDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeOptionCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var paramList = GetParameterList(context.Node, out var body);
        if (paramList is null)
        {
            return;
        }

        var stringParameters = new List<ParameterSyntax>();
        foreach (var param in paramList.Parameters)
        {
            if (param.Type is PredefinedTypeSyntax { Keyword.ValueText: "string" })
            {
                stringParameters.Add(param);
            }
        }

        if (stringParameters.Count == 0)
        {
            return;
        }

        // Strategy 1: name pattern. It applies only when there is no body to inspect
        // (interface and abstract methods, empty-body stubs, record primary constructors).
        // A non-empty body uses Strategy 2, so a display-only method with a path-like
        // parameter name is not flagged on the name alone.
        if (body is null || body.Statements.Count == 0)
        {
            ReportNamePatternMatches(context, stringParameters);
            return;
        }

        // Strategy 2: scan the body once for both the local-root map and the System.IO call
        // sites. Every string parameter then reuses that one scan.
        var localRoots = CollectLocalRoots(body);
        var sites = CollectIoSites(context, body);

        foreach (var param in stringParameters)
        {
            var name = param.Identifier.ValueText;
            if (FindSiteForParameter(name, localRoots, sites) is not { } useSite)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule,
                param.Identifier.GetLocation(),
                PropertiesForDescription(useSite.Description),
                name, useSite.Description, useSite.Type));
        }
    }

    private static ParameterListSyntax? GetParameterList(SyntaxNode node, out BlockSyntax? body)
    {
        switch (node)
        {
            case MethodDeclarationSyntax method:
                body = method.Body;
                return method.ParameterList;
            case ConstructorDeclarationSyntax ctor:
                body = ctor.Body;
                return ctor.ParameterList;
            case RecordDeclarationSyntax { ParameterList: { } recordParams }:
                // A primary constructor has no body to inspect, so only the name pattern applies.
                body = null;
                return recordParams;
            default:
                body = null;
                return null;
        }
    }

    private static void ReportNamePatternMatches(
        SyntaxNodeAnalysisContext context,
        List<ParameterSyntax> stringParameters)
    {
        foreach (var param in stringParameters)
        {
            var name = param.Identifier.ValueText;
            if (PathNamePatterns.IsPathName(name))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule,
                    param.Identifier.GetLocation(),
                    AmbiguousProperties,
                    name, "file system path", "FileInfo' or 'DirectoryInfo"));
            }
        }
    }

    // Maps each local to the names it is derived from: an identifier initializer copies its
    // source, and a Path.* call at argument 0 derives from its first argument. A second pass
    // adds one more hop, so a local built from a derived local also counts.
    private static Dictionary<string, HashSet<string>> CollectLocalRoots(BlockSyntax body)
    {
        var roots = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var pathCalls = new List<(string Local, string Source)>();

        foreach (var node in body.DescendantNodes())
        {
            if (node is not LocalDeclarationStatementSyntax statement)
            {
                continue;
            }

            foreach (var variable in statement.Declaration.Variables)
            {
                var localName = variable.Identifier.ValueText;
                var rhs = variable.Initializer?.Value;

                if (rhs is IdentifierNameSyntax id)
                {
                    AddRoot(roots, localName, id.Identifier.ValueText);
                }
                else if (rhs is InvocationExpressionSyntax init
                         && FileSystemPathHelpers.IsPathMethodCall(init)
                         && init.ArgumentList.Arguments.Count > 0
                         && init.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax source)
                {
                    AddRoot(roots, localName, source.Identifier.ValueText);
                    pathCalls.Add((localName, source.Identifier.ValueText));
                }
            }
        }

        foreach (var (local, source) in pathCalls)
        {
            if (!roots.TryGetValue(source, out var sourceRoots))
            {
                continue;
            }

            foreach (var root in sourceRoots)
            {
                AddRoot(roots, local, root);
            }
        }

        return roots;
    }

    private static void AddRoot(Dictionary<string, HashSet<string>> roots, string local, string root)
    {
        if (!roots.TryGetValue(local, out var set))
        {
            roots[local] = set = new HashSet<string>(StringComparer.Ordinal);
        }

        set.Add(root);
    }

    private static List<(string ArgName, string Description, string Type)> CollectIoSites(
        SyntaxNodeAnalysisContext context,
        BlockSyntax body)
    {
        var sites = new List<(string ArgName, string Description, string Type)>();

        foreach (var node in body.DescendantNodes())
        {
            var site = node switch
            {
                InvocationExpressionSyntax invocation => GetInvocationSite(context, invocation),
                ObjectCreationExpressionSyntax creation => GetCreationSite(context, creation),
                _ => null
            };

            if (site is { } found)
            {
                sites.Add(found);
            }
        }

        return sites;
    }

    private static (string ArgName, string Description, string Type)? FindSiteForParameter(
        string paramName,
        Dictionary<string, HashSet<string>> localRoots,
        List<(string ArgName, string Description, string Type)> sites)
    {
        foreach (var site in sites)
        {
            if (string.Equals(site.ArgName, paramName, StringComparison.Ordinal)
                || (localRoots.TryGetValue(site.ArgName, out var roots) && roots.Contains(paramName)))
            {
                return site;
            }
        }

        return null;
    }

    private static (string ArgName, string Description, string Type)? GetInvocationSite(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        if (memberAccess.Expression is not IdentifierNameSyntax classId)
        {
            return null;
        }

        if (!IoMethodCatalog.TryGetMethodInfo(
                classId.Identifier.ValueText,
                memberAccess.Name.Identifier.ValueText,
                out var info))
        {
            return null;
        }

        var args = invocation.ArgumentList.Arguments;
        if (info.ArgIndex >= args.Count)
        {
            return null;
        }

        if (args[info.ArgIndex].Expression is not IdentifierNameSyntax argId)
        {
            return null;
        }

        // Confirm via semantic model that the call is truly System.IO.
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        var isSystemIo = symbolInfo.Symbol is IMethodSymbol methodSymbol
                         && string.Equals(
                             methodSymbol.ContainingType?.ContainingNamespace?.ToDisplayString(),
                             "System.IO",
                             StringComparison.Ordinal);

        return isSystemIo
            ? info.Suggestion == SuggestedType.FileInfo
                ? (argId.Identifier.ValueText, "file path", "FileInfo")
                : (argId.Identifier.ValueText, "directory path", "DirectoryInfo")
            : null;
    }

    private static (string ArgName, string Description, string Type)? GetCreationSite(
        SyntaxNodeAnalysisContext context,
        ObjectCreationExpressionSyntax creation)
    {
        if (creation.Type is not IdentifierNameSyntax typeId)
        {
            return null;
        }

        if (!IoMethodCatalog.TryGetConstructorInfo(typeId.Identifier.ValueText, out var suggestion))
        {
            return null;
        }

        var argList = creation.ArgumentList;
        if (argList is null || argList.Arguments.Count == 0)
        {
            return null;
        }

        if (argList.Arguments[0].Expression is not IdentifierNameSyntax argId)
        {
            return null;
        }

        // Confirm via semantic model that the constructor is truly System.IO.
        var symbolInfo = context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken);
        var isSystemIo = symbolInfo.Symbol is IMethodSymbol ctorSymbol
                         && string.Equals(
                             ctorSymbol.ContainingType?.ContainingNamespace?.ToDisplayString(),
                             "System.IO",
                             StringComparison.Ordinal);

        return isSystemIo
            ? suggestion == SuggestedType.FileInfo
                ? (argId.Identifier.ValueText, "file path", "FileInfo")
                : (argId.Identifier.ValueText, "directory path", "DirectoryInfo")
            : null;
    }

    private static void AnalyzeOptionCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ObjectCreationExpressionSyntax creation)
        {
            return;
        }

        var genericType = FileSystemPathHelpers.ExtractGenericCliType(creation.Type);
        if (genericType is null)
        {
            return;
        }

        var typeName = genericType.Identifier.ValueText;

        var stringTypeArg = FileSystemPathHelpers.GetCliStringTypeArg(genericType);
        if (stringTypeArg is null)
        {
            return;
        }

        if (!FileSystemPathHelpers.TryGetPathCliName(creation.ArgumentList, out var rawName, out var strippedName))
        {
            return;
        }

        var rule = string.Equals(typeName, "Argument", StringComparison.Ordinal) ? ArgumentRule : OptionRule;
        var (description, suggestion) = FileSystemPathHelpers.GetCliSuggestion(strippedName, typeName);
        context.ReportDiagnostic(Diagnostic.Create(rule,
            stringTypeArg.GetLocation(),
            PropertiesForDescription(description),
            rawName, description, suggestion));
    }

    private static ImmutableDictionary<string, string?> PropertiesForDescription(string description)
    {
        return string.Equals(description, "file path", StringComparison.Ordinal) ? FileInfoProperties
            : string.Equals(description, "directory path", StringComparison.Ordinal) ? DirectoryInfoProperties
            : AmbiguousProperties;
    }
}
