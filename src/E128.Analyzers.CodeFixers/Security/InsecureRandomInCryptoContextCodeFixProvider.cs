using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Security;

/// <summary>
///     Replaces <c lang="csharp">Random.Shared.Next()</c>, <c lang="csharp">Next(max)</c> and
///     <c lang="csharp">Next(min, max)</c> with the <c lang="csharp">RandomNumberGenerator.GetInt32</c> overload that has
///     the same bounds. The fix is not offered for any other Random member, or unless the bounds are compile-time
///     constants that describe a non-empty range, because <c lang="csharp">GetInt32</c> throws for an empty range where
///     <c lang="csharp">Random.Next</c> returns the lower bound. Named arguments map to
///     <c lang="csharp">GetInt32</c> positions through the parameters of the bound <c lang="csharp">Random.Next</c>
///     overload, so <c lang="csharp">Next(maxValue: 7, minValue: 1)</c> becomes
///     <c lang="csharp">GetInt32(1, 7)</c>. The replacement draws from a different random source on purpose, so values
///     differ from the original. The replacement binds to the BCL
///     <c lang="csharp">System.Security.Cryptography.RandomNumberGenerator</c> before the fix is registered.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InsecureRandomInCryptoContextCodeFixProvider))]
[Shared]
public sealed class InsecureRandomInCryptoContextCodeFixProvider : E128CodeFixProviderBase
{
    private const string RandomNumberGeneratorTypeName = "System.Security.Cryptography.RandomNumberGenerator";

    private const string RandomNumberGeneratorName = "RandomNumberGenerator";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [InsecureRandomInCryptoContextAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var shared = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        // Random.Shared.Next(...): the diagnostic covers 'Random.Shared'.
        if (shared is not MemberAccessExpressionSyntax
            || shared.Parent is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Next" } next
            || next.Expression != shared
            || next.Parent is not InvocationExpressionSyntax invocation
            || invocation.Expression != next)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model is null
            || model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsRandomNext(method)
            || !TryGetBounds(invocation, method, model, context.CancellationToken, out var bounds))
        {
            return;
        }

        // Bind the replacement where it will go, so a source type that shadows RandomNumberGenerator, or a
        // missing GetInt32 overload, means no fix rather than code that does not compile.
        var replacement = CreateReplacement(invocation, bounds);
        if (!BindsToGetInt32(model, invocation, replacement))
        {
            return;
        }

        CodeFixRegistration.Register(context, diagnostic, "Use RandomNumberGenerator.GetInt32", "UseGetInt32", ct => Task.FromResult(Replace(context.Document, root, invocation, replacement)));
    }

    private static Document Replace(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        InvocationExpressionSyntax replacement)
    {
        var changed = root.ReplaceNode(invocation, replacement);

        // Add the using directive only when a plain RandomNumberGenerator reference does not already resolve to it.
        var alreadyImported = changed.DescendantNodes()
            .Any(node => node is UsingDirectiveSyntax { Name: { } imported } &&
                imported.ToString() is "System.Security.Cryptography" or "System.Security.Cryptography.*");

        var newRoot = alreadyImported ? changed : UsingDirectives.AddIfMissing(changed, "System.Security.Cryptography");

        // AddIfMissing leaves the newline that separated the old usings from the first member, which
        // doubles as a blank line. The fix keeps the file shape the pre-change ImportAdder output had.
        if (newRoot is CompilationUnitSyntax unit &&
            unit.Usings.Count > 0 &&
            unit.Members.FirstOrDefault() is { } firstMember &&
            firstMember.GetLeadingTrivia().Count > 0 &&
            firstMember.GetLeadingTrivia().All(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
        {
            newRoot = unit.ReplaceNode(firstMember, firstMember.WithLeadingTrivia(SyntaxFactory.TriviaList()));
        }

        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    ///     Returns true when the replacement binds to an accessible static <c lang="csharp">GetInt32</c> overload of the
    ///     referenced <c lang="csharp">System.Security.Cryptography.RandomNumberGenerator</c>, with one int parameter per
    ///     argument.
    /// </summary>
    private static bool BindsToGetInt32(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        InvocationExpressionSyntax replacement)
    {
        var expected = model.Compilation.GetTypeByMetadataName(RandomNumberGeneratorTypeName);
        if (expected is null ||
            SymbolEqualityComparer.Default.Equals(expected.ContainingAssembly, model.Compilation.Assembly))
        {
            return false;
        }

        // The emitted replacement uses the plain name, which resolves only after the using directive lands. It
        // must not bind, at the fix site, to a source type that shadows RandomNumberGenerator.
        var boundPlain = model.GetSpeculativeSymbolInfo(
            invocation.SpanStart,
            replacement,
            SpeculativeBindingOption.BindAsExpression).Symbol;
        if (boundPlain is ITypeSymbol { Name: "RandomNumberGenerator" } plainType &&
            !SymbolEqualityComparer.Default.Equals(plainType, expected))
        {
            return false;
        }

        // The API verification needs a binding that ignores the missing using, so it probes the global-qualified form.
        var probe = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression("global::" + RandomNumberGeneratorTypeName),
                SyntaxFactory.IdentifierName("GetInt32")),
            replacement.ArgumentList);
        var boundGlobal = model.GetSpeculativeSymbolInfo(
            invocation.SpanStart,
            probe,
            SpeculativeBindingOption.BindAsExpression).Symbol;

        return boundGlobal is IMethodSymbol { Name: "GetInt32", IsStatic: true } getInt32
               && SymbolEqualityComparer.Default.Equals(getInt32.ContainingType, expected)
               && getInt32.Parameters.Length == replacement.ArgumentList.Arguments.Count
               && getInt32.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_Int32)
               && model.IsAccessible(invocation.SpanStart, getInt32);
    }

    private static bool IsRandomNext(IMethodSymbol method)
    {
        if (method.IsStatic ||
            !string.Equals(method.Name, "Next", StringComparison.Ordinal) ||
            !FipsSymbolHelpers.IsType(method.ContainingType, "Random", "System"))
        {
            return false;
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type.SpecialType != SpecialType.System_Int32 || parameter.RefKind != RefKind.None)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Reads the arguments of <c lang="csharp">Next()</c>, <c lang="csharp">Next(max)</c> or
    ///     <c lang="csharp">Next(min, max)</c> in parameter order, so a named argument lands where
    ///     <c lang="csharp">GetInt32</c> expects it. The result is empty for <c lang="csharp">Next()</c>. It succeeds only
    ///     when every bound is a compile-time constant after the implicit conversion to int, each parameter receives
    ///     exactly one argument, no argument is <c lang="csharp">ref</c> or <c lang="csharp">out</c>, and the range is not
    ///     empty. A variable bound could be zero or equal to the other bound, and <c lang="csharp">GetInt32</c> throws
    ///     there where <c lang="csharp">Random.Next</c> returns a value. Constant bounds also mean the arguments have no
    ///     side effects.
    /// </summary>
    private static bool TryGetBounds(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ImmutableArray<ExpressionSyntax> bounds)
    {
        bounds = default;

        if (!TryOrderArguments(invocation, method, out var ordered))
        {
            return false;
        }

        var values = new int[ordered.Length];
        var builder = ImmutableArray.CreateBuilder<ExpressionSyntax>(ordered.Length);
        for (var i = 0; i < ordered.Length; i++)
        {
            var expression = ordered[i];
            if (expression is null ||
                !TryGetConstantInt(model.GetConstantValue(expression, cancellationToken), out values[i]))
            {
                return false;
            }

            builder.Add(expression);
        }

        // Next(max) accepts 0 and GetInt32(0) throws, so the range must hold at least one value.
        var isNonEmpty = values.Length switch
        {
            0 => true,
            1 => values[0] > 0,
            2 => values[1] > values[0],
            _ => false
        };

        if (!isNonEmpty)
        {
            return false;
        }

        bounds = builder.MoveToImmutable();

        return true;
    }

    /// <summary>
    ///     Places each argument at its parameter position, so a named argument lands where the fix expects it.
    ///     Returns false when an argument is <c lang="csharp">ref</c> or <c lang="csharp">out</c>, when the count
    ///     does not match the parameter count, or when a parameter receives more than one argument.
    /// </summary>
    private static bool TryOrderArguments(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        out ExpressionSyntax?[] ordered)
    {
        ordered = [];

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != method.Parameters.Length)
        {
            return false;
        }

        ordered = new ExpressionSyntax?[arguments.Count];
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (!argument.RefKindKeyword.IsKind(SyntaxKind.None))
            {
                return false;
            }

            var ordinal = argument.NameColon is { } nameColon
                ? IndexOfParameter(method, nameColon.Name.Identifier.ValueText)
                : i;

            if (ordinal < 0 || ordered[ordinal] is not null)
            {
                return false;
            }

            ordered[ordinal] = argument.Expression;
        }

        return true;
    }

    private static int IndexOfParameter(IMethodSymbol method, string name)
    {
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (string.Equals(method.Parameters[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Reads a constant that converts implicitly to int: <c lang="csharp">int</c>, <c lang="csharp">short</c>,
    ///     <c lang="csharp">ushort</c>, <c lang="csharp">byte</c>, <c lang="csharp">sbyte</c>, or <c lang="csharp">char</c>.
    /// </summary>
    private static bool TryGetConstantInt(Optional<object?> constant, out int value)
    {
        int? converted = constant.HasValue
            ? constant.Value switch
            {
                int i => i,
                short s => s,
                ushort u => u,
                byte b => b,
                sbyte sb => sb,
                char c => c,
                _ => null
            }
            : null;

        value = converted.GetValueOrDefault();

        return converted is not null;
    }

    private static InvocationExpressionSyntax CreateReplacement(
        InvocationExpressionSyntax invocation,
        ImmutableArray<ExpressionSyntax> bounds)
    {
        var arguments = bounds.IsEmpty
            ? SyntaxFactory.SeparatedList(new[] { SyntaxFactory.Argument(SyntaxFactory.ParseExpression("int.MaxValue")) })
            : SyntaxFactory.SeparatedList(
                bounds.Select(bound => SyntaxFactory.Argument(bound.WithoutTrivia())),
                Enumerable.Repeat(
                    SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space),
                    bounds.Length - 1));

        return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseExpression(RandomNumberGeneratorName),
                    SyntaxFactory.IdentifierName("GetInt32")),
                SyntaxFactory.ArgumentList(arguments))
            .WithTriviaFrom(invocation);
    }
}
