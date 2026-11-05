using System;
using System.Collections.Immutable;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128165: Flags a credential written as a string literal in production code. The rule reads two shapes:
///     a literal bound to a credential name such as <c lang="csharp">password</c> or
///     <c lang="csharp">apiKey</c>, and a connection string that carries a password.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HardcodedSecretAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128165";

    private const int MinimumSecretLength = 8;

    private static readonly string[] CredentialWords =
    [
        "password",
        "passwd",
        "pwd",
        "secret",
        "apikey",
        "accesskey",
        "privatekey",
        "connectionstring",
        "token",
        "credential",
        "bearer"
    ];

    // A name that ends in one of these words names a lookup key, a header, or a label. The value is
    // not the credential.
    private static readonly string[] NonCredentialSuffixes =
    [
        "name",
        "header",
        "claim",
        "type",
        "prefix",
        "scheme",
        "path",
        "label",
        "format",
        "field",
        "param",
        "parameter",
        "setting",
        "settingkey",
        "configkey",
        "configurationkey",
        "pattern",
        "regex",
        "arn",
        "id",
        "count",
        "length",
        "expiry",
        "hint"
    ];

    private static readonly string[] ServerKeys = ["server", "data source", "host"];

    private static readonly string[] PasswordKeys = ["password", "pwd"];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Avoid hardcoded secret",
        "Hardcoded credential in '{0}': expose it through an options class bound to configuration",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A password, key, or connection string in source ships with every copy of the code. Expose the value through a strongly-typed options class bound to configuration.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (XunitTestHelper.IsTestCompilation(start.Compilation))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                AnalyzeLiteral,
                SyntaxKind.StringLiteralExpression,
                SyntaxKind.Utf8StringLiteralExpression);
        });
    }

    private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var value = literal.Token.ValueText;

        if (HasConnectionStringPassword(value))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, literal.GetLocation(), "connection string"));
            return;
        }

        if (value.Length < MinimumSecretLength || ContainsWhitespace(value) || LooksLikeKeyName(value))
        {
            return;
        }

        var name = TargetName(literal);

        if (name is not null && IsCredentialName(name))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, literal.GetLocation(), name));
        }
    }

    private static string? TargetName(LiteralExpressionSyntax literal)
    {
        SyntaxNode current = literal;

        // Look through parentheses, a coalesce, and the branches of a conditional.
        while (current.Parent is { } parent && IsTransparent(parent, current))
        {
            current = parent;
        }

        return current.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.ValueText,
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Identifier.ValueText,
            ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Identifier.ValueText,
            ArrowExpressionClauseSyntax { Parent: AccessorDeclarationSyntax accessor } => GetterPropertyName(accessor),
            ReturnStatementSyntax returnStatement => ReturnPropertyName(returnStatement),
            AssignmentExpressionSyntax assignment when assignment.Right == current => AssignmentTargetName(assignment.Left),
            ArgumentSyntax { NameColon: { } nameColon } => nameColon.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static bool IsTransparent(SyntaxNode parent, SyntaxNode child)
    {
        return parent switch
        {
            ParenthesizedExpressionSyntax => true,
            BinaryExpressionSyntax binary => binary.IsKind(SyntaxKind.CoalesceExpression),
            ConditionalExpressionSyntax conditional => conditional.Condition != child,
            _ => false
        };
    }

    private static string? AssignmentTargetName(ExpressionSyntax left)
    {
        return left switch
        {
            ElementAccessExpressionSyntax element => ConstantKey(element.ArgumentList),
            ImplicitElementAccessSyntax implicitElement => ConstantKey(implicitElement.ArgumentList),
            _ => SimpleName(left)
        };
    }

    // An indexer key such as ["Password"] names the credential.
    private static string? ConstantKey(BracketedArgumentListSyntax argumentList)
    {
        return argumentList.Arguments.Count == 1
               && argumentList.Arguments[0].Expression is LiteralExpressionSyntax key
               && key.IsKind(SyntaxKind.StringLiteralExpression)
            ? key.Token.ValueText
            : null;
    }

    // A return statement counts only when it sits in the getter of a property.
    private static string? ReturnPropertyName(ReturnStatementSyntax returnStatement)
    {
        foreach (var ancestor in returnStatement.Ancestors())
        {
            if (ancestor is AccessorDeclarationSyntax accessor)
            {
                return GetterPropertyName(accessor);
            }

            if (ancestor is BaseMethodDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return null;
            }
        }

        return null;
    }

    private static string? GetterPropertyName(AccessorDeclarationSyntax accessor)
    {
        return accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
               && accessor.Parent?.Parent is PropertyDeclarationSyntax property
            ? property.Identifier.ValueText
            : null;
    }

    // A header name ("X-Api-Key", "Some-Header") or a config path ("Auth:Password") labels a
    // setting. It is not the secret. A secret mixes digits and symbols or uses lowercase words.
    private static bool LooksLikeKeyName(string value)
    {
        if (value.StartsWith("X-", StringComparison.OrdinalIgnoreCase) || IsScreamingSnakeCase(value))
        {
            return true;
        }

        var hasPathSeparator = false;

        foreach (var character in value)
        {
            if (character is ':' or '/' or '.')
            {
                hasPathSeparator = true;
            }
            else if (!char.IsLetter(character) && character is not '-' and not '_')
            {
                return false;
            }
        }

        return hasPathSeparator || IsTitleCaseHyphenated(value);
    }

    // "BRAVE_API_KEY" names the setting a credential is read from. The underscore requirement keeps
    // a plain uppercase secret, such as "LETMEIN", in scope for the rule.
    private static bool IsScreamingSnakeCase(string value)
    {
        var hasLetter = false;
        var hasUnderscore = false;

        foreach (var character in value)
        {
            if (character == '_')
            {
                hasUnderscore = true;
                continue;
            }

            if (!char.IsLetter(character) || !char.IsUpper(character))
            {
                return false;
            }

            hasLetter = true;
        }

        return hasLetter && hasUnderscore;
    }

    private static bool IsTitleCaseHyphenated(string value)
    {
        var segments = value.Split('-');

        if (segments.Length < 2)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0 || !char.IsUpper(segment[0]))
            {
                return false;
            }
        }

        return true;
    }

    private static string? SimpleName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static bool IsCredentialName(string name)
    {
        var normalized = name.Replace("_", string.Empty);

        // A cancellation token is not a credential.
        if (normalized.IndexOf("cancellation", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        foreach (var suffix in NonCredentialSuffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var word in CredentialWords)
        {
            if (normalized.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    // A connection string is a list of key=value pairs separated by semicolons. Read each key
    // so that a URL query string such as "?host=a&password=b" does not match.
    private static bool HasConnectionStringPassword(string value)
    {
        if (value.IndexOf(';') < 0)
        {
            return false;
        }

        var hasServer = false;
        var hasPassword = false;

        foreach (var pair in value.Split(';'))
        {
            var separator = pair.IndexOf('=');

            if (separator <= 0)
            {
                continue;
            }

            var key = pair.Substring(0, separator).Trim();
            var pairValue = pair.Substring(separator + 1).Trim();

            if (IsKey(key, ServerKeys))
            {
                hasServer = true;
            }
            else if (IsKey(key, PasswordKeys) && pairValue.Length > 0 && pairValue[0] != '{')
            {
                // A format placeholder or an empty value is not a hardcoded password.
                hasPassword = true;
            }
        }

        return hasServer && hasPassword;
    }

    private static bool IsKey(string key, string[] keys)
    {
        foreach (var candidate in keys)
        {
            if (string.Equals(key, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsWhitespace(string value)
    {
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }
}
