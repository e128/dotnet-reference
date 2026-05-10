using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.FileSystem;

internal static class FileSystemPathHelpers
{
    internal static HashSet<string> CollectPathDerivedLocals(BlockSyntax body, string paramName)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var descendant in body.DescendantNodes())
        {
            if (descendant is not LocalDeclarationStatementSyntax statement)
            {
                continue;
            }

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
        foreach (var descendant in body.DescendantNodes())
        {
            if (descendant is not LocalDeclarationStatementSyntax statement)
            {
                continue;
            }

            foreach (var variable in statement.Declaration.Variables)
            {
                var localName = variable.Identifier.ValueText;
                if (result.Contains(localName))
                {
                    continue;
                }

                if (variable.Initializer?.Value is InvocationExpressionSyntax init
                    && IsPathMethodCall(init)
                    && HasAnyFirstArgumentNamed(init.ArgumentList, result))
                {
                    toAdd.Add(localName);
                }
            }
        }

        result.UnionWith(toAdd);
        return result;
    }

    internal static bool HasAnyFirstArgumentNamed(ArgumentListSyntax argumentList, HashSet<string> names)
    {
        if (!argumentList.Arguments.Any())
        {
            return false;
        }

        var firstArgExpr = argumentList.Arguments[0].Expression;
        return firstArgExpr is IdentifierNameSyntax id && names.Contains(id.Identifier.ValueText);
    }

    internal static bool IsPathMethodCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "Path" },
            Name: SimpleNameSyntax methodName
        }
               && IoMethodCatalog.IsPathMethod(methodName.Identifier.ValueText);
    }

    // Checks whether the identifier 'name' appears as the FIRST argument (index 0).
    // Used for path-derivation: only a param/local at arg[0] of Path.Combine / Path.GetXxx
    // is itself a root path; params at arg[1]+ are path segments, not standalone paths.
    internal static bool HasFirstArgumentNamed(ArgumentListSyntax argList, string name)
    {
        var args = argList.Arguments;

        return args.Count != 0
               && args[0].Expression is IdentifierNameSyntax id
               && string.Equals(id.Identifier.ValueText, name, StringComparison.Ordinal);
    }

    // Extracts the GenericNameSyntax for Option<T> or Argument<T> from either:
    //   - Unqualified:  Option<string> / Argument<string>
    //   - Qualified:    System.CommandLine.Option<string> / System.CommandLine.Argument<string>
    internal static GenericNameSyntax? ExtractGenericCliType(TypeSyntax type)
    {
        return type is GenericNameSyntax { Identifier.ValueText: "Option" or "Argument" } direct
            ? direct
            : type is QualifiedNameSyntax qualified
              && qualified.Right is GenericNameSyntax { Identifier.ValueText: "Option" or "Argument" } nested
                ? nested
                : null;
    }

    internal static PredefinedTypeSyntax? GetCliStringTypeArg(GenericNameSyntax genericType)
    {
        var typeArgs = genericType.TypeArgumentList.Arguments;
        return typeArgs.Count != 1 ? null : typeArgs[0] is PredefinedTypeSyntax { Keyword.ValueText: "string" } arg ? arg : null;
    }

    internal static bool TryGetPathCliName(
        ArgumentListSyntax? argList,
        out string rawName,
        out string strippedName)
    {
        rawName = string.Empty;
        strippedName = string.Empty;

        // RCS9004: SeparatedSyntaxList<T>.Count is O(1); calling Any() would allocate an enumerator.
        if (argList is null || argList.Arguments.Count == 0)
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

    // Returns true if the name (dashes stripped) suggests a file system path.
    // Extends PathNamePatterns with CLI-specific terms: "input", "output", and "file"
    // ("file" is intentionally excluded from PathNamePatterns for parameter names to avoid
    // firing on `fileName` string params, but `--file` CLI options are almost always file paths).
    internal static bool IsPathCliName(string name)
    {
        return PathNamePatterns.IsPathName(name)
               || name.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0
               || name.IndexOf("output", StringComparison.OrdinalIgnoreCase) >= 0
               || name.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Returns description and the full suggestion string for the diagnostic message.
    // typeName is "Option" or "Argument" — the suggestion includes the correct generic wrapper.
    internal static (string Description, string Suggestion) GetCliSuggestion(string strippedName, string typeName)
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
