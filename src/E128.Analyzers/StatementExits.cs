using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class StatementExits
{
    /// <summary>
    ///     True when the statement leaves the enclosing scope with
    ///     <c lang="csharp">return</c>, <c lang="csharp">throw</c>, <c lang="csharp">continue</c>, or
    ///     <c lang="csharp">break</c>. A block counts when its last statement exits, and an
    ///     <c lang="csharp">if</c> counts when both branches exit.
    /// </summary>
    internal static bool EndsInExit(StatementSyntax statement)
    {
        switch (statement)
        {
            case BlockSyntax block:
                return block.Statements.Count > 0
                       && EndsInExit(block.Statements.Last());

            case IfStatementSyntax ifStatement:
                return ifStatement.Else is not null
                       && EndsInExit(ifStatement.Statement)
                       && EndsInExit(ifStatement.Else.Statement);

            default:
                return statement is ReturnStatementSyntax or ThrowStatementSyntax
                    or ContinueStatementSyntax or BreakStatementSyntax;
        }
    }
}
