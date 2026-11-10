using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128166: Flags an <c lang="csharp">ILogger</c> call that passes a value whose name holds a credential or
///     personal-data word, such as <c lang="csharp">password</c> or <c lang="csharp">token</c>. A log line outlives the
///     request and reaches every reader of the log store.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SensitiveLogArgumentAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128166";

    private const string LoggerMessageAttributeName = "Microsoft.Extensions.Logging.LoggerMessageAttribute";

    // A name that holds one of these words anywhere is sensitive.
    private static readonly string[] SensitiveWords =
    [
        "password",
        "passwd",
        "secret",
        "apikey",
        "accesskey",
        "privatekey",
        "connectionstring",
        "token",
        "creditcard",
        "cardnumber"
    ];

    // A short word matches only as a whole word. A name such as processNumber holds "ssn" inside
    // "process" and "Number", so a substring match would report it by mistake.
    private static readonly string[] SensitiveWholeWords =
    [
        "pwd",
        "ssn"
    ];

    // A name with one of these endings, or the word "cancellation", holds metadata or a framework
    // type, not the sensitive value.
    private static readonly string[] SafeSuffixes =
    [
        "name",
        "type",
        "count",
        "length",
        "expiry",
        "header",
        "id",
        "hint"
    ];

    // The logger, the event id, and the level types carry no template value.
    private static readonly string[] FrameworkTypeNames =
    [
        "Microsoft.Extensions.Logging.EventId",
        "Microsoft.Extensions.Logging.LogLevel",
        "Microsoft.Extensions.Logging.ILogger",
        "Microsoft.Extensions.Logging.ILogger<TCategoryName>"
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Avoid logging a sensitive value",
        "Logger call passes '{0}', a credential or personal-data value: log an identifier or a redacted form instead",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A log entry outlives the request and reaches every reader of the log store. Log an identifier or a redacted form of a credential or personal-data value.");

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
            if (XunitTestHelper.IsTestCompilation(start.Compilation, start.CancellationToken))
            {
                return;
            }

            start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        var isGenerated = HasLoggerMessageAttribute(method);

        if (!isGenerated && !IsLoggerMethod(method))
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            AnalyzeArgument(context, argument, isGenerated);
        }
    }

    private static void AnalyzeArgument(OperationAnalysisContext context, IArgumentOperation argument, bool isGenerated)
    {
        if (argument.Parameter is not { } parameter
            || argument.ArgumentKind is ArgumentKind.DefaultValue
            || IsFrameworkParameter(parameter.Type))
        {
            return;
        }

        var value = StripConversions(argument.Value);

        // The params object[] holds the template values. Read each element, whether the call
        // spells the array out or lets the compiler build it.
        if (parameter.IsParams && value is IArrayCreationOperation { Initializer: { } initializer })
        {
            ReportElements(context, initializer);

            return;
        }

        if (argument.Value.Syntax is not ExpressionSyntax syntax)
        {
            return;
        }

        // A template string is not a value. Only the holes of an interpolated template can
        // carry one. A LoggerMessage method has no template parameter, so every string is a value.
        if (!isGenerated && parameter.Type.SpecialType is SpecialType.System_String)
        {
            if (syntax is InterpolatedStringExpressionSyntax)
            {
                Report(context, ValueExpressions(syntax), argument.Syntax.GetLocation());
            }

            return;
        }

        Report(context, ValueExpressions(syntax), argument.Syntax.GetLocation());
    }

    private static void ReportElements(OperationAnalysisContext context, IArrayInitializerOperation initializer)
    {
        foreach (var element in initializer.ElementValues)
        {
            // The element keeps the syntax the author wrote, so a cast stays visible.
            if (element.Syntax is ExpressionSyntax elementSyntax)
            {
                Report(context, [elementSyntax], ElementLocation(elementSyntax));
            }
        }
    }

    private static void Report(
        OperationAnalysisContext context,
        IEnumerable<ExpressionSyntax> expressions,
        Location location)
    {
        foreach (var expression in expressions)
        {
            var name = LeafName(expression);

            if (name is not null && IsSensitiveName(name))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
                return;
            }
        }
    }

    // An element that is a call argument reports at the argument. An element inside an explicit
    // array initializer reports at the element.
    private static Location ElementLocation(ExpressionSyntax element)
    {
        SyntaxNode current = element;

        while (current.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized;
        }

        return current.Parent is ArgumentSyntax argument ? argument.GetLocation() : current.GetLocation();
    }

    private static IOperation StripConversions(IOperation operation)
    {
        var current = operation;

        while (current is IConversionOperation conversion)
        {
            current = conversion.Operand;
        }

        return current;
    }

    private static bool HasLoggerMessageAttribute(IMethodSymbol method)
    {
        return HasAttribute(method)
               || (method.PartialDefinitionPart is { } definition && HasAttribute(definition))
               || (method.PartialImplementationPart is { } implementation && HasAttribute(implementation));
    }

    private static bool HasAttribute(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (string.Equals(
                    attribute.AttributeClass?.ToDisplayString(),
                    LoggerMessageAttributeName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLoggerMethod(IMethodSymbol method)
    {
        var typeName = method.ContainingType.ToDisplayString();

        return string.Equals(typeName, "Microsoft.Extensions.Logging.LoggerExtensions", StringComparison.Ordinal)
               || string.Equals(typeName, "Microsoft.Extensions.Logging.ILogger", StringComparison.Ordinal);
    }

    // The logger, the event id, the level, the exception, and the formatter carry no template value.
    private static bool IsFrameworkParameter(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Delegate)
        {
            return true;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            // The fully qualified format leaves out the nullable annotation, so Exception? still matches.
            if (string.Equals(
                    current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    "global::System.Exception",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return FrameworkTypeNames.Contains(type.OriginalDefinition.ToDisplayString(), StringComparer.Ordinal);
    }

    // An interpolated argument hides its values in holes. Read each hole as its own value.
    private static IEnumerable<ExpressionSyntax> ValueExpressions(ExpressionSyntax expression)
    {
        return expression is InterpolatedStringExpressionSyntax interpolated
            ? interpolated.Contents.OfType<InterpolationSyntax>().Select(static hole => hole.Expression)
            : new[] { expression };
    }

    // Reads the name of the value an expression ends in. A cast, a parenthesis, a null-forgiving
    // operator, a conditional access, and a ToString or Trim call with no arguments keep the name.
    private static string? LeafName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            ConditionalAccessExpressionSyntax access => ConditionalAccessLeafName(access),
            InvocationExpressionSyntax invocation => InvocationLeafName(invocation),
            _ => WrappedExpression(expression) is { } inner ? LeafName(inner) : null
        };
    }

    // The expression inside a parenthesis, a cast, a null-forgiving operator or an "as" expression.
    private static ExpressionSyntax? WrappedExpression(ExpressionSyntax expression)
    {
        return expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
            CastExpressionSyntax cast => cast.Expression,
            PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } postfix =>
                postfix.Operand,
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } asExpression => asExpression.Left,
            _ => null
        };
    }

    private static bool IsToStringOrTrim(SimpleNameSyntax name)
    {
        var text = name.Identifier.ValueText;

        return string.Equals(text, "ToString", StringComparison.Ordinal)
               || string.Equals(text, "Trim", StringComparison.Ordinal);
    }

    private static string? ConditionalAccessLeafName(ConditionalAccessExpressionSyntax access)
    {
        return access.WhenNotNull is InvocationExpressionSyntax
        {
            ArgumentList.Arguments.Count: 0,
            Expression: MemberBindingExpressionSyntax binding
        } && IsToStringOrTrim(binding.Name)
            ? LeafName(access.Expression)
            : LeafName(access.WhenNotNull);
    }

    private static string? InvocationLeafName(InvocationExpressionSyntax invocation)
    {
        return invocation is
        {
            ArgumentList.Arguments.Count: 0,
            Expression: MemberAccessExpressionSyntax call
        } && IsToStringOrTrim(call.Name)
            ? LeafName(call.Expression)
            : null;
    }

    private static bool IsSensitiveName(string name)
    {
        var normalized = name.Replace("_", string.Empty);

        if (normalized.IndexOf("cancellation", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        foreach (var suffix in SafeSuffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var word in SensitiveWords)
        {
            if (normalized.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        foreach (var part in WordSplitter.Split(name, "_"))
        {
            foreach (var word in SensitiveWholeWords)
            {
                if (string.Equals(part, word, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
