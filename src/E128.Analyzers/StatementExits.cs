using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class StatementExits
{
    /// <summary>
    ///     True when the statement, or the last statement of a block, leaves the enclosing scope with
    ///     <c lang="csharp">return</c>, <c lang="csharp">throw</c>, <c lang="csharp">continue</c>, or
    ///     <c lang="csharp">break</c>.
    /// </summary>
    internal static bool EndsInExit(StatementSyntax statement)
    {
        var last = statement is BlockSyntax block ? block.Statements.LastOrDefault() : statement;

        return last is ReturnStatementSyntax or ThrowStatementSyntax or ContinueStatementSyntax
            or BreakStatementSyntax;
    }
}
