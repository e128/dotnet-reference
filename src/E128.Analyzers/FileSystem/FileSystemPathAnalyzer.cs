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
        ParameterListSyntax paramList;
        BlockSyntax? body;

        if (context.Node is MethodDeclarationSyntax method)
        {
            paramList = method.ParameterList;
            body = method.Body;
        }
        else if (context.Node is ConstructorDeclarationSyntax ctor)
        {
            paramList = ctor.ParameterList;
            body = ctor.Body;
        }
        else if (context.Node is RecordDeclarationSyntax record)
        {
            // Primary constructor — no body to inspect; name-pattern strategy only.
            if (record.ParameterList is null)
            {
                return;
            }

            paramList = record.ParameterList;
            body = null;
        }
        else
        {
            return;
        }

        foreach (var param in paramList.Parameters)
        {
            if (param.Type is not PredefinedTypeSyntax { Keyword.ValueText: "string" })
            {
                continue;
            }

            AnalyzeStringParameter(context, param, body);
        }
    }

    // Strategy 1: name pattern — fires when there is no body to inspect (interface /
    // abstract methods, empty-body stubs, or record primary constructors). A non-empty body
    // falls through to Strategy 2 so that display-only or non-IO methods (e.g. WriteHeader,
    // GetFts5CountAsync) are not flagged on the basis of a path-like parameter name alone.
    // Strategy 2: use-site walk — fires when the parameter (or a local derived from it via
    // Path.* at arg[0]) is passed directly to a System.IO method or constructor.
    private static void AnalyzeStringParameter(
        SyntaxNodeAnalysisContext context,
        ParameterSyntax param,
        BlockSyntax? body)
    {
        var name = param.Identifier.ValueText;

        if (PathNamePatterns.IsPathName(name) && (body is null || !body.Statements.Any()))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule,
                param.Identifier.GetLocation(),
                AmbiguousProperties,
                name, "file system path", "FileInfo' or 'DirectoryInfo"));
            return;
        }

        if (body is null)
        {
            return;
        }

        // Compute path-derived locals for this specific parameter (one-hop via Path.*).
        var pathDerivedLocals = FileSystemPathHelpers.CollectPathDerivedLocals(body, name);
        var useSite = FindUseSiteSuggestion(context, body, name, pathDerivedLocals);

        if (useSite is not null)
        {
            var (description, suggestedType) = useSite.Value;
            context.ReportDiagnostic(Diagnostic.Create(Rule,
                param.Identifier.GetLocation(),
                PropertiesForDescription(description),
                name, description, suggestedType));
        }
    }

    private static (string Description, string Type)? FindUseSiteSuggestion(
        SyntaxNodeAnalysisContext context,
        BlockSyntax body,
        string paramName,
        HashSet<string> pathDerivedLocals)
    {
        foreach (var node in body.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax invocation)
            {
                var result = CheckInvocation(context, invocation, paramName, pathDerivedLocals);
                if (result is not null)
                {
                    return result;
                }
            }
            else if (node is ObjectCreationExpressionSyntax creation)
            {
                var result = CheckObjectCreation(context, creation, paramName, pathDerivedLocals);
                if (result is not null)
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static (string Description, string Type)? CheckInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string paramName,
        HashSet<string> pathDerivedLocals)
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

        var argName = argId.Identifier.ValueText;
        if (!string.Equals(argName, paramName, StringComparison.Ordinal)
            && !pathDerivedLocals.Contains(argName))
        {
            return null;
        }

        // Confirm via semantic model that the call is truly System.IO.
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        return symbolInfo.Symbol is not IMethodSymbol methodSymbol
            ? null
            : !string.Equals(
                methodSymbol.ContainingType?.ContainingNamespace?.ToDisplayString(),
                "System.IO",
                StringComparison.Ordinal)
                ? null
                : info.Suggestion == SuggestedType.FileInfo
                    ? ("file path", "FileInfo")
                    : ("directory path", "DirectoryInfo");
    }

    private static (string Description, string Type)? CheckObjectCreation(
        SyntaxNodeAnalysisContext context,
        ObjectCreationExpressionSyntax creation,
        string paramName,
        HashSet<string> pathDerivedLocals)
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
        if (argList is null)
        {
            return null;
        }

        // RCS9004: SeparatedSyntaxList<T>.Count is O(1); calling Any() would allocate an enumerator.
#pragma warning disable RCS9004
        if (argList.Arguments.Count == 0)
#pragma warning restore RCS9004
        {
            return null;
        }

        if (argList.Arguments[0].Expression is not IdentifierNameSyntax argId)
        {
            return null;
        }

        var argName = argId.Identifier.ValueText;
        if (!string.Equals(argName, paramName, StringComparison.Ordinal)
            && !pathDerivedLocals.Contains(argName))
        {
            return null;
        }

        // Confirm via semantic model that the constructor is truly System.IO.
        var symbolInfo = context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken);
        return symbolInfo.Symbol is not IMethodSymbol ctorSymbol
            ? null
            : !string.Equals(
                ctorSymbol.ContainingType?.ContainingNamespace?.ToDisplayString(),
                "System.IO",
                StringComparison.Ordinal)
                ? null
                : suggestion == SuggestedType.FileInfo
                    ? ("file path", "FileInfo")
                    : ("directory path", "DirectoryInfo");
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
