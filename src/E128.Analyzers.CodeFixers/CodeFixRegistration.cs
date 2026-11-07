using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace E128.Analyzers;

/// <summary>
///     Registers a code action for one diagnostic. The equivalence key is mandatory, so fix-all can
///     separate one action from a sibling action on the same diagnostic.
/// </summary>
internal static class CodeFixRegistration
{
    /// <summary>
    ///     Registers an action that changes one document.
    /// </summary>
    internal static void Register(
        CodeFixContext context,
        Diagnostic diagnostic,
        string title,
        string equivalenceKey,
        Func<CancellationToken, Task<Document>> createChangedDocument)
    {
        context.RegisterCodeFix(
            CodeAction.Create(title, createChangedDocument, equivalenceKey),
            diagnostic);
    }

    /// <summary>
    ///     Registers an action that changes the whole solution, such as a symbol rename.
    /// </summary>
    internal static void Register(
        CodeFixContext context,
        Diagnostic diagnostic,
        string title,
        string equivalenceKey,
        Func<CancellationToken, Task<Solution>> createChangedSolution)
    {
        context.RegisterCodeFix(
            CodeAction.Create(title, createChangedSolution, equivalenceKey),
            diagnostic);
    }
}
