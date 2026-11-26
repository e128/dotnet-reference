using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace E128.Analyzers;

/// <summary>
///     Shared base for this repository's code fix providers. One place owns the
///     batch fix-all provider and the syntax root fetch that most fixes start with.
///     A provider that does not start from the syntax root overrides
///     <see cref="RegisterCodeFixesAsync"/> directly instead of the hook.
/// </summary>
public abstract class E128CodeFixProviderBase : CodeFixProvider
{
    /// <summary>
    ///     The batch fix-all provider. A fixer whose edits cannot be fix-all'd,
    ///     for example a sequential rename, overrides this to return null.
    /// </summary>
    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        await RegisterFixesAsync(context, root).ConfigureAwait(false);
    }

    /// <summary>
    ///     The provider-specific work, called only when the syntax root resolved. A
    ///     provider that registers from the diagnostic alone overrides
    ///     <see cref="RegisterCodeFixesAsync"/> directly and never reaches this hook.
    /// </summary>
    protected virtual Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Registers one document-changing action for the first diagnostic. The node under the
    ///     diagnostic span comes from <c lang="csharp">root.FindNode</c>. When that node is not
    ///     <typeparamref name="TNode"/>, nothing registers. Otherwise
    ///     <paramref name="createChangedDocument"/> receives the typed node and produces the fix.
    /// </summary>
    /// <typeparam name="TNode">The syntax node type the diagnostic node must have.</typeparam>
    protected static void RegisterNodeFix<TNode>(
        CodeFixContext context,
        SyntaxNode root,
        string title,
        string equivalenceKey,
        Func<TNode, CancellationToken, Task<Document>> createChangedDocument)
        where TNode : SyntaxNode
    {
        RegisterNodeFix(context, root, title, equivalenceKey, createChangedDocument, getInnermostNodeForTie: false);
    }

    /// <summary>
    ///     The form of <see cref="RegisterNodeFix{TNode}(CodeFixContext, SyntaxNode, string, string,
    ///     Func{TNode, CancellationToken, Task{Document}})"/> that resolves a span tie to the
    ///     innermost node.
    /// </summary>
    /// <typeparam name="TNode">The syntax node type the diagnostic node must have.</typeparam>
    /// <exception cref="ArgumentNullException">The caller passed a null root.</exception>
    protected static void RegisterNodeFix<TNode>(
        CodeFixContext context,
        SyntaxNode root,
        string title,
        string equivalenceKey,
        Func<TNode, CancellationToken, Task<Document>> createChangedDocument,
        bool getInnermostNodeForTie)
        where TNode : SyntaxNode
    {
        if (root is null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie);

        if (node is not TNode typed)
        {
            return;
        }

        CodeFixRegistration.Register(
            context,
            diagnostic,
            title,
            equivalenceKey,
            cancellationToken => createChangedDocument(typed, cancellationToken));
    }
}
