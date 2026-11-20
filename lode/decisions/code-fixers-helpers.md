# Code Fixer Decisions: Shared Helpers
*Updated: 2026-11-20T17:16:51Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## DiRegistrationFix
- Builds type names with ToMinimalDisplayString. That form qualifies a name only when it is out of scope, escapes keyword identifiers, and matches the text in the action title.

## FixSyntax
- FixSyntax.InsertFieldAsFirstMember moves the first member's leading trivia onto a new field. The first member keeps its own leading trivia so a doc comment stays attached to it.

## UsingDirectives
- Moves a file header onto the new directive. A licence comment is leading trivia of the first node. As a result, it stays at the top whether it sat on a using or on the first member.

Related: [Decision Records](summary.md) explains how to use this page.
