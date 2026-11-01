# Code Quality Plan: Review Findings, 2026-11-01

*Updated: 2026-11-01T00:00:00Z*

Merge of one `mr-sparkle` run. Target: commits newer than 1 day on `main`
(base `71ac63c2..c9f53fa`), 299 files, +12636/−1700, 8 commits. This plan
holds the confirmed High finding and the confirmed Medium and Low findings.
Application order: Phase 1 first, then Phase 2, then the phoenix items last.

## Findings

| # | File | Line | Problem | Sev |
|---|------|------|---------|-----|
| 1 | scripts/internal/cr-target.sh | 241, 265, 276 | Untracked files reach `files` but `diff_command git diff $BASE` emits no content for them, and `scripts/diff.sh --files` omits untracked files from days and commits lists. Reviewers never see new-file content. | High |
| 2 | src/E128.Analyzers/README.md | 1361 | E128132 section claims SortedDictionary and SortedSet coverage. The analyzer registers none of the two and a test pins the silence. | Med |
| 3 | src/E128.Analyzers/README.md | 534-536 | E128040 section and the rule table row still describe the old zero-is-defect boundary. The analyzer changed to `value < 0`. | Med |
| 4 | scripts/lode-check.sh | 42 | `--changed` builds the file list from `git diff --name-only` alone. A new lode file skips the style lint and the 250-line guard. Verified live. | Med |
| 5 | src/E128.Analyzers.CodeFixers/.editorconfig | 5 | `dotnet_diagnostic.RCS9004.severity = none` is a new downgrade. No user approval recorded. See AGENTS.md. | Med |
| 6 | src/E128.Analyzers/Performance/AlternateLookupAnalyzer.cs | 101 | Any first argument of a string-keyed collection call counts as a key. `ContainsValue` reports a false positive. | Med |
| 7 | src/E128.Analyzers/Style/NonXmlDocCommentE128Analyzer.cs | 52 | Skip fires when any leading trivia is a pragma directive. A `restore` also skips, so a comment after the restore goes unchecked. | Med |
| 8 | scripts/internal/cr-target.sh | 250 | Working-tree last-commit fallback suppresses the `git diff` failure and reports an empty target shape. | Med |
| 9 | .claude/rules/deterministic-scripts.md | routing table | No row for the new `scripts/git-forensics.sh`. | Med |
| 10 | scripts/dotnet-reference-go.sh | 7 | Endpoint URL and default model hardcoded in source. | Med |
| 11 | README.md | 189 | "(fully qualified name required)" is false. `test.sh` accepts bare names. | Low |
| 12 | scripts/git-forensics.sh | 28 | `--top` accepts any string, no numeric check. | Low |
| 13 | scripts/build.sh | 44 | `--errors` prints nothing when the failure has no `: error ` line. | Low |
| 14 | plans/review-2026-11-01-code-quality/code-quality-tasks.md | jq fold item | Item marked `[x]` without a matching code change. | Low |
| 15 | scripts/lode-check.sh | 88 | printf JSON does not escape embedded quotes or backslashes. | Low |
| 16 | scripts/internal/cr-target.sh | 157 | `$ROOT` is interpolated unescaped into a sed pattern. | Low |
| 17 | scripts/internal/cr-target.sh | 225, 241, 344 | A newline in a PR file name fuses entries in `files`. Already deferred in the existing plan. | Low |
| 18 | src/E128.Analyzers.CodeFixers/.editorconfig | routing | `git-forensics.sh` and the three `dotnet-reference*.sh` launchers have no routing-table rows. | Low |
| 19 | tests/E128.Analyzers.Tests/ArrayPoolSqliteParameterSizeCodeFixTests.cs | 131 | E128086 emits an unchecked TODO byte count. | Low |

Simplification findings and deferred observations live in
`general-tasks.md` under their own sections.

## Phases

### Phase 1: Review-scope integrity

1. `cr-target.sh`: pair `files` with a content source. Emit untracked
   files with `git diff --no-index` into the delivered diff. Union
   `git ls-files --others --exclude-standard` in `days` and `commits`
   list construction.
2. Fix the swallowed `git diff` failure in the last-commit fallback.
3. Union `git ls-files --others --exclude-standard -- lode` into
   `lode-check.sh --changed`.
4. Add a test for the untracked-file path and for PR-mode resolution
   against a mocked `gh` fixture.

### Phase 2: Documentation truth

1. E128132: delete the SortedDictionary and SortedSet claims, or add
   their names to the analyzer and flip the silence test.
2. E128040: rewrite the README section, the table row, and the analyzer
   docstring. Zero is a legal SemaphoreSlim initial count.
3. README test-runner paragraph: drop the false "fully qualified name
   required" claim.

### Phase 3: Rule and process compliance

1. RCS9004: record user approval in the plan or remove the downgrade.
   This needs a user decision.
2. AlternateLookupAnalyzer: restrict reporting to key positions.
3. E128024: skip only after a matching disable, not after any pragma.
4. `git-forensics.sh`: validate `--top` as a positive integer.
5. `build.sh --errors`: fall back to the output tail when no
   `: error ` line matches.
6. Uncheck the jq-fold plan item or perform the fold.

### Phase 4: Phoenix items

Low-priority shell and emission robustness. Schedule as time permits.
