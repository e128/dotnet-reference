# Analyzer Decisions: Style
*Updated: 2026-11-20T17:16:51Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## EmptyStringLiteralAnalyzer
- Skips compile-time constant contexts. string.Empty is a static field read rather than a constant. As a result, it cannot replace a literal in a const field, const local, default parameter value, switch label, or pattern.
- Treats goto case labels and default parameter values as const contexts. Both require a compile-time constant. As a result, string.Empty would not compile there and the rule must not suggest it.

## MidNameUnderscoreAnalyzer
- Returns early for the implementation part of a partial member. Roslyn raises the symbol action once per declaring part. As a result, the guard prevents a duplicate diagnostic.

## NonXmlDocCommentE128Analyzer
- Ignores a comment covered by an open #pragma warning disable. The code fix removes the comment, which would drop the suppression reason and trip IDE0079. As a result, the rule leaves it alone. Only a disable counts. This holds because a restore that ends an earlier block lands in the next member's leading trivia.

## ObsoleteWithoutMessageAnalyzer
- Finds the message argument by position or by name. ObsoleteAttribute takes the message as the first positional argument, but a named message argument can appear in any order. As a result, the rule checks both.
- Treats a non-constant message expression as a message and a blank constant as missing. A null or blank constant names no replacement, while a computed value cannot be judged statically and must not be reported.

## PragmaBalanceAnalyzer
- Applies directives in document order. A restore clears only a disable that appears before it. As a result, a file that restores first and disables later leaves the suppression open.
- Treats a bare restore as clearing every open suppression. A #pragma warning restore with no error codes restores all warnings. As a result, the open set is cleared.
- Treats a bare disable as unbalanced until a bare restore appears. A #pragma warning disable with no error codes turns off every warning. As a result, no narrower restore can close it.

## PragmaFileScopedNamespaceAnalyzer
- Walks backwards from the namespace token and stops at the first match. The earlier implementation walked every token in the file, including the body below the namespace, which cost time on large files.

## SuppressionAuditAnalyzer
- Stops the preceding-comment scan at a blank line. In Roslyn trivia a blank line is two consecutive EndOfLineTrivia entries. As a result, the check must test the earlier entry as well.

## UnjustifiedSuppressionAttributeAnalyzer
- Resolves every type with the checked metadata name. Two references can declare the same attribute type. As a result, the lookup must keep every match instead of the first.
- Checks each resolved attribute type for a Justification property. ExcludeFromCodeCoverageAttribute.Justification exists only on .NET 5 and later. As a result, an attribute type without the property is skipped.
- Treats a non-constant justification as present and a blank constant as missing. A null or blank constant gives no reason, while a computed value cannot be judged statically and must not be reported.

## UntrackedTodoAnalyzer
- Counts a marker only at the start of a comment line. Prose such as "this avoids a hack" must not report. As a result, the marker match trims comment decoration and tests the start of each line.

Related: [Decision Records](summary.md) explains how to use this page.
