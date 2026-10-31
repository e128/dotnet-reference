# Code Quality Review Tasks

*Updated: 2026-10-31T21:01:10Z*

## Phase 1: Repair the E128141 code fix

- [x] `src/E128.Analyzers/Performance/Utf8GetBytesLiteralAnalyzer.cs:154` A `Span<byte>` target takes the bare literal, which does not compile. Fix: accept only `ReadOnlySpan<byte>` for the bare literal. Severity: High. Source: security-reviewer, code-reviewer, adversarial, quality
- [x] `src/E128.Analyzers/Performance/Utf8GetBytesLiteralCodeFixProvider.cs:67` The emit path builds `literal + "u8"` for every byte span. Fix: emit `u8.ToArray()` when the target is `Span<byte>`. Severity: High. Source: code-reviewer
- [x] `src/E128.Analyzers/README.md:1450` The prose states the `Span<byte>` bare-literal behaviour as intended. Fix: say `ReadOnlySpan<byte>` only. Severity: Medium. Source: functionality

## Phase 2: Repair the E128140 semantics

- [x] `src/E128.Analyzers/Performance/ElementLoopToBulkSpanAnalyzer.cs:121` A zero literal becomes `Clear()`, which writes `default(T)`. Fix: route to `Fill` unless the element type makes both forms equal. Severity: Medium. Source: security-reviewer, functionality
- [x] `src/E128.Analyzers/Performance/ElementLoopToBulkSpanAnalyzer.cs:222` `IsSimpleTarget` accepts a property getter or a nested call. Fix: resolve the receiver symbol and require a local, parameter, or field. Severity: Medium. Source: functionality, quality

## Phase 3: Harden the analyzer guards

- [x] `src/E128.Analyzers/Performance/Utf8GetBytesLiteralAnalyzer.cs:116` A hard cast of `SyntaxTree.Options` can throw. Fix: use the `is not CSharpParseOptions` pattern. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Performance/Utf8GetBytesLiteralAnalyzer.cs:131` The positional index ignores a named argument. Fix: map `NameColon` to the parameter name first. Severity: Medium. Source: security-reviewer
- [x] `src/E128.Analyzers/Unsafe/UnsafeUnalignedNonBlittableAnalyzer.cs:82` The message renders an empty name for an array type argument. Fix: use `ToDisplayString`. Severity: Medium. Source: functionality
- [x] `src/E128.Analyzers/Performance/ElementLoopToBulkSpanAnalyzer.cs:22` `KindKey` has no reader. Fix: delete it, or read it in the code fix. Severity: Medium. Source: code-reviewer, quality
- [x] `src/E128.Analyzers/Unsafe/UnsafeUnalignedNonBlittableCodeFixProvider.cs:78` The fix emits `(source != 0);` as a statement, which is CS0201. Fix: handle the expression statement parent. Severity: Medium. Source: code-review

## Phase 4: Cover every repaired guard

- [x] `tests/E128.Analyzers.Tests/Utf8GetBytesLiteralCodeFixTests.cs:54` No test uses a `Span<byte>` target. Fix: add a parameter case and a local case. Severity: Medium. Source: functionality, quality
- [x] `tests/E128.Analyzers.Tests/Utf8GetBytesLiteralAnalyzerTests.cs:16` The C# 11 gate has no test. Fix: add a C# 10 silence case. Severity: Low. Source: functionality, quality
- [x] `tests/E128.Analyzers.Tests/ElementLoopToBulkSpanAnalyzerTests.cs:20` No test uses a nullable element type. Fix: add an `int?[]` zero-fill case. Severity: Medium. Source: functionality
- [x] `tests/E128.Analyzers.Tests/UnsafeUnalignedNonBlittableAnalyzerTests.cs:26` The `in` and `ref readonly` overload has no test. Fix: add one. Severity: Low. Source: defender

## Phase 5: Repair the documentation

- [x] `lode/infrastructure/claude-code-maintenance.md:183` The lode claims a prompt pointer that does not exist. Fix: add the pointer to the prompt, or drop the claim. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/README.md:194` The E128139 row is 126 characters and its neighbours are 125. Fix: remove one pad space. Severity: Low. Source: code-reviewer, quality
- [x] `lode/dotnet/analyzer-candidates.md:114` The third-read table has no common column width. Fix: widen the columns and repad every row. Severity: Low. Source: code-reviewer, quality
- [x] `lode/dotnet/analyzer-candidates.md:112` Prose says every rule ships a code fix and the table disagrees. Fix: state the current mapping only. Severity: Low. Source: code-reviewer
- [x] `lode/dotnet/analyzer-candidates.md:49` The lode carries dated change history. Fix: state the current state only. Severity: Low. Source: quality
- [x] `lode/dotnet/analyzer-candidates.md:106` The lode says E128139 never held an ID, and `HEAD` assigned it to the `Debug.Assert` guard. Fix: restore the true history. Severity: Medium. Source: code-review
- [x] `prompts/SystemPrompt.txt:164` The prompt says the lint script owns the checklist, and the judgment checks live in `SKILL.md`. Fix: name the skill file. Severity: Medium. Source: code-review
- [x] `lode/dotnet/analyzers.md:114` A 48-word row exceeds the descriptive sentence cap. Fix: split the row. Severity: Low. Source: code-review
- [x] `src/E128.Analyzers/README.md:1440` The prose says constant string and the rule matches a literal only. Fix: say literal string, or accept a constant. Severity: Low. Source: functionality, quality

## Phase 6: Delivery hygiene

- [ ] `src/E128.Analyzers/README.md:1452` Nineteen unrelated rule sections ride in this change. Fix: move them to their own change. Severity: Low. Source: quality. Not applied: the backfill documents rules that already ship, so it ships as its own commit ahead of this one.
- [x] `tests/E128.Analyzers.Tests/UnsafeUnalignedNonBlittableAnalyzerTests.cs:22` A null-forgiving operator breaches a repo gate. Fix: use a null guard. Severity: Low. Source: security-reviewer
- [ ] `src/E128.Analyzers/Performance/ElementLoopToBulkSpanAnalyzer.cs:346` Two switch default arms are unreachable. Fix: delete them and the dead empty-text guard. Severity: Low. Source: quality. Not applied: IDE0072 and SS018 require an exhaustive switch, so the arms stay. The empty-text guard reads `match.Value` and `match.Source`, which are nullable by design.
- [x] `src/E128.Analyzers/Performance/ElementLoopToBulkSpanAnalyzer.cs:110` `BulkLoopMatch` forces meaningless constructor arguments. Fix: carry one payload expression. Severity: Low. Source: code-reviewer, quality
- [x] `src/E128.Analyzers/Unsafe/UnsafeUnalignedNonBlittableCodeFixProvider.cs:46` The fix identifies `bool` by syntax text. Fix: use a `PredefinedTypeSyntax` check. Severity: Low. Source: quality
