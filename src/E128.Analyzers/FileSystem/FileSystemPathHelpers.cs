using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.FileSystem;

internal static class FileSystemPathHelpers
{
    internal static bool IsPathMethodCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax pathType,
            Name: SimpleNameSyntax methodName
        }
               && string.Equals(pathType.Identifier.ValueText, "Path", StringComparison.Ordinal)
               && IoMethodCatalog.IsPathMethod(methodName.Identifier.ValueText);
    }

    // Extracts the GenericNameSyntax for Option<T> or Argument<T> from either:
    //   - Unqualified:  Option<string> / Argument<string>
    //   - Qualified:    System.CommandLine.Option<string> / System.CommandLine.Argument<string>
    internal static GenericNameSyntax? ExtractGenericCliType(TypeSyntax type)
    {
        if (type is GenericNameSyntax direct && IsCliTypeName(direct.Identifier.ValueText))
        {
            return direct;
        }

        return type is QualifiedNameSyntax { Right: GenericNameSyntax nested }
            && IsCliTypeName(nested.Identifier.ValueText)
            ? nested
            : null;
    }

    private static bool IsCliTypeName(string name)
    {
        return string.Equals(name, "Option", StringComparison.Ordinal)
               || string.Equals(name, "Argument", StringComparison.Ordinal);
    }

    internal static PredefinedTypeSyntax? GetCliStringTypeArg(GenericNameSyntax genericType)
    {
        var typeArgs = genericType.TypeArgumentList.Arguments;
        if (typeArgs.Count is not 1)
        {
            return null;
        }

        return typeArgs[0] is PredefinedTypeSyntax arg
            && string.Equals(arg.Keyword.ValueText, "string", StringComparison.Ordinal)
            ? arg
            : null;
    }

    internal static bool TryGetPathCliName(
        ArgumentListSyntax? argList,
        out string rawName,
        out string strippedName)
    {
        rawName = string.Empty;
        strippedName = string.Empty;

        // RCS9004: SeparatedSyntaxList<T>.Count is O(1); calling Any() would allocate an enumerator.
        if (argList is null || argList.Arguments.Count is 0)
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
