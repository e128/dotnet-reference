# Review Tasks: days 2 Union Review

*Updated: 2026-11-14T00:42:34Z*

- [x] `src/E128.Analyzers.CodeFixers/Reliability/GeneratedRegexNestedCodeFixProvider.cs:53` Removing the outer `*` changes the pattern language for the single-atom body. Fix: strip the quantifier only when the outer is `+` or the inner is `*`; add a `(a+)*` test. Severity: High. Source: security-reviewer, code-reviewer, adversarial-review.
- [x] `src/E128.Analyzers/Reliability/GeneratedRegexHelpers.cs:289` The atom comparison does not expand character classes, so a catastrophic group is exempted. Fix: expand class contents in the comparison, or state the limit in the README and fix the slash test. Severity: High. Source: adversarial-review, code-reviewer.
- [x] `src/E128.Analyzers/Reliability/HandRolledProcessCaptureAnalyzer.cs:156` A start-info local set after creation never binds, because `FlagBinds` never calls `LocalLinksToProcess`. Fix: route a local receiver through `LocalLinksToProcess`; add assignment-shape tests. Severity: High. Source: code-reviewer, security-reviewer, adversarial-review, code-review.
- [x] `src/E128.Analyzers/Performance/ReadOnlyMemoryStreamAnalyzer.cs:57-66,232-234` Compression streams count as read-only consumers in `Compress` mode. Fix: require the `CompressionMode.Decompress` argument for the three wrapper types. Severity: High. Source: code-reviewer, security-reviewer.
- [x] `src/E128.Analyzers/Security/HttpBodyReadWithoutSizeBoundAnalyzer.cs:142-157` Bound detection matches identifier text, so a parameterless `LoadIntoBufferAsync()` or an unrelated `ContentLength` silences the rule. Fix: resolve the invocation symbol and require the capped overload or a null-safe comparison before the read. Severity: High. Source: code-reviewer, security-reviewer, adversarial-review, code-review.
- [x] `src/E128.Analyzers/README.md:3274-3286` The documented `ContentLength` guard fails open on a chunked response. Fix: document a real bound and update `Silent_WhenContentLengthIsCheckedFirst`. Severity: High. Source: adversarial-review, security-reviewer.
- [x] `src/E128.Analyzers/Design/UnionSwitchDiscardArmAnalyzer.cs:124-147` A `when`-guarded discard counts as a catch-all, and the fix creates a non-exhaustive switch. Fix: treat a guarded arm as not exhaustive; add a `_ when cond =>` test. Severity: High. Source: security-reviewer, adversarial-review, code-review, code-reviewer.
- [x] `lode/dotnet/release-tracking.md:170` The release table ends at 3.0.0, but the head commit ships 3.1.0 with E128227. Fix: add the 3.1.0 row and bump the timestamp. Severity: High. Source: code-reviewer.

Related: [plan](general-plan.md), [context](general-context.md).
