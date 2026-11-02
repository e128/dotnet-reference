# Tasks: Review Findings, 2026-11-01

- [x] `scripts/internal/cr-target.sh:241,265,276` Untracked files reach `files` but `diff_command` emits no content for them, and days and commits lists omit untracked files entirely. Fix: emit `git diff --no-index` per untracked path into the delivered diff, and union `git ls-files --others --exclude-standard` into the `days` and `commits` list construction. Severity: High. Source: defender.
- [x] `scripts/internal/cr-target.sh:250` Last-commit fallback suppresses the `git diff` failure and reports an empty target shape. Fix: capture the exit code and route non-zero through `err`. Severity: Medium. Source: functionality-attacker, code-review.
- [x] `scripts/lode-check.sh:42` `--changed` misses new lode files because `git diff` excludes untracked files. Fix: union the diff output with `git ls-files --others --exclude-standard -- lode`. Severity: Medium. Source: ponytail, functionality-attacker, code-review.
- [x] `src/E128.Analyzers/README.md:1361` E128132 claims SortedDictionary and SortedSet coverage the analyzer does not implement. Fix: correct the README, or add the two names to `CultureStringComparerAnalyzer.Initialize` and flip `Silent_WhenTheCollectionSortsByCulture`. Severity: Medium. Source: code-reviewer, ponytail, quality-attacker, code-review.
- [x] `src/E128.Analyzers/README.md:534` and `ConcurrencyLimitAnalyzer.cs` docstring E128040 wording still says zero is a defect. Fix: rewrite for the new `value < 0` boundary. Severity: Medium. Source: ponytail, quality-attacker, code-review.
- [x] `src/E128.Analyzers.CodeFixers/.editorconfig:5` RCS9004 downgrade approved by the maintainer, 2026-11-01, and recorded in the .editorconfig comment.
- [x] `src/E128.Analyzers/Performance/AlternateLookupAnalyzer.cs:101` First argument of any collection call counts as a key. Fix: restrict to key positions. Severity: Medium. Source: code-review.
- [x] `src/E128.Analyzers/Style/NonXmlDocCommentE128Analyzer.cs:52` Pragma skip also fires on restore. Fix: require a preceding disable. Severity: Medium. Source: ponytail, code-review.
- [x] `.claude/rules/deterministic-scripts.md` No routing row for `git-forensics.sh`. Fix: add the row. Severity: Medium. Source: dotnet-lens.
- [x] `scripts/dotnet-reference-go.sh:7` Endpoint URL and default model hardcoded. Fix: constant in lode-lib.sh or a config variable. Severity: Medium. Source: dotnet-lens.
- [x] `README.md:189` "(fully qualified name required)" is false. Fix: drop the parenthetical. Severity: Low. Source: ponytail, quality-attacker.
- [x] `scripts/git-forensics.sh:28` `--top` accepts any string. Fix: validate positive integer. Severity: Low. Source: ponytail, code-review.
- [x] `scripts/build.sh:44` `--errors` silent when no `: error ` line. Fix: fall back to the output tail. Severity: Low. Source: functionality-attacker.
- [x] `plans/review-2026-11-01-code-quality/code-quality-tasks.md` jq-fold item marked done but the code is unchanged. Fix: uncheck, or perform the fold. Severity: Low. Source: ponytail.
- [x] `scripts/lode-check.sh:88` printf JSON does not escape path characters. Fix: build with `jq -n --arg`. Severity: Low. Source: security-reviewer (3 agents).
- [x] `scripts/internal/cr-target.sh:157` `$ROOT` interpolation into sed breaks on meta-chars. Fix: parameter expansion. Severity: Low. Source: security, adversarial-security.
- [ ] `scripts/internal/cr-target.sh:225,344` Newline in a PR file name fuses entries. Fix: NUL-delimited fields. Severity: Low. Source: security-reviewer, adversarial-security.
- [ ] `tests/E128.Analyzers.Tests/ArrayPoolSqliteParameterSizeCodeFixTests.cs:131` E128086 `Size = 0` TODO emission. Fix: derive used length or decline. Severity: Low. Source: code-reviewer.

## Simplifications

- [x] `tests/E128.Analyzers.Tests/*VerifyFixAsync/VerifyNoFixAsync*` copied across 102 and 20 files respectively. Fix: one shared generic test helper, remove each copy. Source: ponytail.
- [x] `src/E128.Analyzers.CodeFixers/*` scaffolding (`GetFixAllProvider`, `RegisterCodeFixesAsync` head) repeated ~107 times. Fix: one `E128CodeFixProviderBase`. Source: ponytail.
- [x] more code fix providers than the 9 itemized ones bypass `UsingDirectives.AddIfMissing`. Fix: rewrite on touch. Source: ponytail.
- [x] Both analyzers ship an identical `FindType`. Fix: keep one and delete the other. Source: ponytail.
- [x] `WithAllowUnsafe` transform duplicated across 12 test files. Fix: shared test helper. Source: ponytail.
- [x] AngleSharp and Tensor stubs duplicated with content drift across paired test files. Fix: shared const. Source: ponytail.
- [x] `lode-guard.sh` and `lode-check.sh` each own the 250 limit. Fix: single constant. Source: ponytail.
- [x] `IsParameterlessCreation`, `IsInStaticMember`, and the member-indent trivia block each duplicated across two to three fixer files. Fix: shared helper in `FixSyntax.cs`. Source: ponytail.
- [x] `InModifierHelper` modifier-rank table and the PragmaBalance bare-restore fix branch have no failing test on a regression. Fix: one table test, one emission test. Source: ponytail.

## Deferred

- [ ] `scripts/test.sh:89` exit-3 gate covers an explicitly empty trait run and `--all` naming `trait CI` despite no filter. Fix: gate the branch on a supplied filter. Severity: Low. Source: ponytail, dotnet-lens.
- [ ] `scripts/internal/cr-target.sh` PR resolution paths untested. Fix: mock `gh` behind a command variable. Source: functionality-attacker.
- [ ] `scripts/internal/cr-target.sh:120` the `critical` `only` word drop does not implement the adjacent-pair rule the comment states. Fix: match the pair or rewrite the comment. Severity: Low. Source: functionality-attacker.
- [ ] `XmlDocCommentAnalyzer.cs` note the name and line as shipped today. Severity: Low. Source: code-reviewer.
- [ ] `NonPublicBclReflectionAnalyzer.cs` assembly-name prefix match over-selects user assemblies. Fix: BCL identity set. Severity: Low. Source: code-reviewer.
- [ ] `ManualBitCountCodeFixProvider.cs:60` replacement built by string concat. Fix: SyntaxFactory nodes. Severity: Low. Source: code-reviewer.
- [ ] `Utf8StringParseAnalyzer.cs` Initialize local named `context` and `context2`. Fix: rename. Severity: Low. Source: quality-attacker.
- [ ] `ManualBitCountAnalyzer` skips unsigned counters by design. Severity: Low. Source: dotnet-lens.
- [ ] `SequentialRenameFixAllProvider` drops stale spans silently. Severity: Low. Source: dotnet-lens.
- [ ] The 250 duplicate implementations across each fixer file have no lode-tracking entry. Severity: Low. Source: ponytail.

## Context

Merged from 11 reviewers across the run. Everything else is parked in
`general-context.md`. Mark items done only when a commit also updates the
matched plan phases downstream.
