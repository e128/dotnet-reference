using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128161: replaces <c lang="csharp">Path.Combine</c> with
///     <c lang="csharp">Path.Join</c>, which keeps every segment.
/// </summary>
/// <remarks>
///     The fix applies to a rooted segment only. <c lang="csharp">Path.Join</c> keeps a parent
///     segment as written, so the joined path still escapes the base directory and the fix would
///     silence the report without removing the defect. <c lang="csharp">Path.Join</c> also needs
///     .NET Core 2.1 or .NET Standard 2.1, so the fix stays absent when the target lacks it.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PathCombineSegmentCodeFixProvider))]
[Shared]
public sealed class PathCombineSegmentCodeFixProvider : E128CodeFixProviderBase
{
    private const string PathTypeName = "System.IO.Path";

    private const string JoinMethodName = "Join";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [PathCombineSegmentAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        var argument = node.FirstAncestorOrSelf<ArgumentSyntax>();

        if (invocation?.Expression is not MemberAccessExpressionSyntax { Name: { } methodName }
            || argument is null)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (!IsFixable(argument, model, context.CancellationToken))
        {
            return;
        }

        CodeFixRegistration.Register(context, diagnostic, $"Use {PathCombineSegmentAnalyzer.ReplacementMethodName}", nameof(PathCombineSegmentCodeFixProvider), ct => ReplaceAsync(context.Document, methodName, ct));
    }

    private static bool IsFixable(ArgumentSyntax argument, SemanticModel? model, CancellationToken cancellationToken)
    {
        return model is not null
               && model.GetConstantValue(argument.Expression, cancellationToken) is { HasValue: true, Value: string segment }
               && PathCombineSegmentAnalyzer.IsRooted(segment)
               && HasJoinMethod(model.Compilation);
    }

    private static bool HasJoinMethod(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName(PathTypeName)?.GetMembers(JoinMethodName).Length > 0;
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        SimpleNameSyntax methodName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        var replacement = methodName.WithIdentifier(SyntaxFactory.Identifier(JoinMethodName))
            .WithTriviaFrom(methodName);

        return document.WithSyntaxRoot(root.ReplaceNode(methodName, replacement));
    }
}
