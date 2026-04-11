using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.FileSystem;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileSystemPathAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = "E128001";

    // messageFormat placeholders: {0}=paramName, {1}=description, {2}=suggestedType.
    // For the ambiguous (name-only) case, {2} is "FileInfo' or 'DirectoryInfo" — the surrounding
    // single-quotes in the format string produce: Consider using 'FileInfo' or 'DirectoryInfo' instead of 'string'.
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Use FileInfo or DirectoryInfo instead of string for file system paths",
        messageFormat: "Parameter '{0}' appears to represent a {1}. Consider using '{2}' instead of 'string'.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // messageFormat: {0}=optionName (e.g. "--input"), {1}=description, {2}=full suggestion (e.g. "'Option<DirectoryInfo>'").
    private static readonly DiagnosticDescriptor OptionRule = new(
        id: DiagnosticId,
        title: "Use Option<FileInfo> or Option<DirectoryInfo> instead of Option<string> for file system path options",
        messageFormat: "Option '{0}' appears to represent a {1}. Consider using {2} instead of 'Option<string>'.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // messageFormat: {0}=argumentName (e.g. "path"), {1}=description, {2}=full suggestion (e.g. "'Argument<DirectoryInfo>'").
    private static readonly DiagnosticDescriptor ArgumentRule = new(
        id: DiagnosticId,
        title: "Use Argument<FileInfo> or Argument<DirectoryInfo> instead of Argument<string> for file system path arguments",
        messageFormat: "Argument '{0}' appears to represent a {1}. Consider using {2} instead of 'Argument<string>'.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

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
                name, "file system path", "FileInfo' or 'DirectoryInfo"));
            return;
        }

        if (body is null)
        {
            return;
        }

        // Compute path-derived locals for this specific parameter (one-hop via Path.*).
        var pathDerivedLocals = CollectPathDerivedLocals(body, name);
        var useSite = FindUseSiteSuggestion(context, body, name, pathDerivedLocals);

        if (useSite is not null)
        {
            var (description, suggestedType) = useSite.Value;
            context.ReportDiagnostic(Diagnostic.Create(Rule,
                param.Identifier.GetLocation(),
                name, description, suggestedType));
        }
    }

    private static HashSet<string> CollectPathDerivedLocals(BlockSyntax body, string paramName)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var statements = body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().ToList();

        foreach (var statement in statements)
        {
            foreach (var variable in statement.Declaration.Variables)
            {
                var rhs = variable.Initializer?.Value;
                var localName = variable.Identifier.ValueText;
                var isDerived = rhs is IdentifierNameSyntax id
                    && string.Equals(id.Identifier.ValueText, paramName, StringComparison.Ordinal);
                var isPathDerived = !isDerived
                    && rhs is InvocationExpressionSyntax init
                    && IsPathMethodCall(init)
                    && HasFirstArgumentNamed(init.ArgumentList, paramName);
                if (isDerived || isPathDerived)
                {
                    result.Add(localName);
                }
            }
        }

        var toAdd = new HashSet<string>(StringComparer.Ordinal);
        foreach (var statement in statements)
        {
            foreach (var variable in statement.Declaration.Variables)
            {
                var localName = variable.Identifier.ValueText;
                if (result.Contains(localName))
                {
                    continue;
                }

                if (variable.Initializer?.Value is InvocationExpressionSyntax init
                    && IsPathMethodCall(init)
                    && result.Any(known => HasFirstArgumentNamed(init.ArgumentList, known)))
                {
                    toAdd.Add(localName);
                }
            }
        }

        result.UnionWith(toAdd);
        return result;
    }

    private static bool IsPathMethodCall(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "Path" },
            Name: SimpleNameSyntax methodName,
        }
        && IoMethodCatalog.IsPathMethod(methodName.Identifier.ValueText);

    // Checks whether the identifier 'name' appears as the FIRST argument (index 0).
    // Used for path-derivation: only a param/local at arg[0] of Path.Combine / Path.GetXxx
    // is itself a root path; params at arg[1]+ are path segments, not standalone paths.
    private static bool HasFirstArgumentNamed(ArgumentListSyntax argList, string name)
    {
        var args = argList.Arguments;

        // RCS9004: SeparatedSyntaxList<T>.Count is O(1).
#pragma warning disable RCS9004
        if (args.Count == 0)
#pragma warning restore RCS9004
        {
            return false;
        }

        return args[0].Expression is IdentifierNameSyntax id
            && string.Equals(id.Identifier.ValueText, name, StringComparison.Ordinal);
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

        var genericType = ExtractGenericCliType(creation.Type);
        if (genericType is null)
        {
            return;
        }

        var typeName = genericType.Identifier.ValueText;

        if (!TryGetCliStringTypeArg(genericType, out var stringTypeArg))
        {
            return;
        }

        if (!TryGetPathCliName(creation.ArgumentList, out var rawName, out var strippedName))
        {
            return;
        }

        var rule = string.Equals(typeName, "Argument", StringComparison.Ordinal) ? ArgumentRule : OptionRule;
        var (description, suggestion) = GetCliSuggestion(strippedName, typeName);
        context.ReportDiagnostic(Diagnostic.Create(rule,
            stringTypeArg.GetLocation(),
            rawName, description, suggestion));
    }

    private static bool TryGetCliStringTypeArg(
        GenericNameSyntax genericType,
        out PredefinedTypeSyntax stringTypeArg)
    {
        stringTypeArg = default!;
        var typeArgs = genericType.TypeArgumentList.Arguments;
        if (typeArgs.Count != 1)
        {
            return false;
        }

        if (typeArgs[0] is not PredefinedTypeSyntax { Keyword.ValueText: "string" } arg)
        {
            return false;
        }

        stringTypeArg = arg;
        return true;
    }

    private static bool TryGetPathCliName(
        ArgumentListSyntax? argList,
        out string rawName,
        out string strippedName)
    {
        rawName = string.Empty;
        strippedName = string.Empty;

        // RCS9004: SeparatedSyntaxList<T>.Count is O(1); calling Any() would allocate an enumerator.
#pragma warning disable RCS9004
        if (argList is null || argList.Arguments.Count == 0)
#pragma warning restore RCS9004
        {
            return false;
        }

        if (argList.Arguments[0].Expression is not LiteralExpressionSyntax literal
            || !literal.Token.IsKind(SyntaxKind.StringLiteralToken))
        {
            return false;
        }

        rawName = literal.Token.ValueText;
        strippedName = rawName.TrimStart('-');
        return !string.IsNullOrEmpty(strippedName) && IsPathCliName(strippedName);
    }

    // Extracts the GenericNameSyntax for Option<T> or Argument<T> from either:
    //   - Unqualified:  Option<string> / Argument<string>
    //   - Qualified:    System.CommandLine.Option<string> / System.CommandLine.Argument<string>
    private static GenericNameSyntax? ExtractGenericCliType(TypeSyntax type)
    {
        return type is GenericNameSyntax { Identifier.ValueText: "Option" or "Argument" } direct
            ? direct
            : type is QualifiedNameSyntax qualified
            && qualified.Right is GenericNameSyntax { Identifier.ValueText: "Option" or "Argument" } nested
            ? nested
            : null;
    }

    // Returns true if the name (dashes stripped) suggests a file system path.
    // Extends PathNamePatterns with CLI-specific terms: "input", "output", and "file"
    // ("file" is intentionally excluded from PathNamePatterns for parameter names to avoid
    // firing on `fileName` string params, but `--file` CLI options are almost always file paths).
    private static bool IsPathCliName(string name) =>
        PathNamePatterns.IsPathName(name)
        || name.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("output", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0;

    // Returns description and the full suggestion string for the diagnostic message.
    // typeName is "Option" or "Argument" — the suggestion includes the correct generic wrapper.
    private static (string Description, string Suggestion) GetCliSuggestion(string strippedName, string typeName)
    {
        if (strippedName.IndexOf("dir", StringComparison.OrdinalIgnoreCase) >= 0
            || strippedName.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return ("directory path", $"'{typeName}<DirectoryInfo>'");
        }

        if (strippedName.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return ("file path", $"'{typeName}<FileInfo>'");
        }

        // Ambiguous (path, input, output, etc.) — suggest either.
        return ("file system path", $"'{typeName}<FileInfo>' or '{typeName}<DirectoryInfo>'");
    }
}
