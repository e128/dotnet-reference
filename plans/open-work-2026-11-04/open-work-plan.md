# Open Work Plan, 2026-11-04

*Updated: 2026-11-04T20:40:40Z*

Source: the e128.info page "Code Review: Twenty Practices That Survived Thirty
Years". Six of the twenty practices map to source rules: #8 (review the tests),
#10 (explain intent), #13 (route risk), #15 (design first), #16 (question
assumptions), and #20 (close the loop). The other fourteen are process. One
becomes a script: the diff size gate for practices #5 and #7.

Sonar, Meziantou, Roslynator, and the Threading analyzers are installed. Each
rule below covers a shape those packages do not.

New IDs start at E128164. Confirm the next free ID with
`scripts/analyzer-context.sh` before each phase.

## Success Criteria

- Each rule reports on its violation snippet and stays silent on its negative cases.
- The README rule table, `AnalyzerReleases.Unshipped.md`, and the test class agree.
- `scripts/check.sh` passes at the end of each phase.
- The matching lode file changes in the same commit.

## Scope

This plan holds all open work as of 2026-11-04. Part 1 is the review-practice
analyzer build (Phases 1 to 5 and the script). Part 2 is the review backlog:
the open Low and deferred findings from the closed plans
`review-2026-11-01-code-quality`, `review-2026-11-01-general`, and
`review-2026-11-03-security`. The proof-test sweep plan
`analyzers-2026-11-04` closed earlier. See [tasks](open-work-tasks.md).

## Phase 1: Hardcoded Configuration (practices #13, #16)

| #   | Rule                                                          | Analyzer                                             | Fix  |
| --- | ------------------------------------------------------------- | ---------------------------------------------------- | ---- |
| 1   | E128164 Hardcoded absolute URL in source                      | `Security/HardcodedUrlAnalyzer.cs`                   | None |
| 2   | E128165 Hardcoded secret or connection string in source       | `Security/HardcodedSecretAnalyzer.cs`                | None |

Notes:

- Both rules enforce the `AGENTS.md` ban on a hardcoded URL, secret, or
  connection string. Neither rule runs in a test project. A compilation that
  references `Xunit.FactAttribute` counts as a test project.
- E128164 reports a string literal that starts with `http://` or `https://`
  and has a host. It skips attribute arguments, XML namespace and schema hosts
  (`w3.org`, `schemas.microsoft.com`, `json-schema.org`), and `example.com`,
  `example.org`, and `example.net`.
- E128165 reports a non-empty literal of 8 or more characters, with no
  space, assigned to a name that holds `password`, `passwd`, `pwd`, `secret`,
  `apikey`, `accesskey`, `privatekey`, or `connectionstring`. It also reports a
  literal that has both a server key (`Server=`, `Data Source=`, `Host=`) and a
  credential key (`Password=`, `Pwd=`). A name that ends in `Name`, `Header`,
  `Claim`, `Type`, `Prefix`, `Scheme`, `Path`, `Label`, `Format`, `Field`,
  `Param`, `Parameter`, or `Setting` names a lookup key and stays silent.

## Phase 2: Sensitive Data In Logs (practice #13)

| #   | Rule                                              | Analyzer                                  | Fix  |
| --- | ------------------------------------------------- | ----------------------------------------- | ---- |
| 3   | E128166 Sensitive value passed to a logger call   | `Security/SensitiveLogArgumentAnalyzer.cs` | None |

Reads an `ILogger` extension or `LoggerMessage` argument whose identifier or
member name holds a credential or personal-data word. Reuses the name list from
E128165.

## Phase 3: Untracked Deferral (practice #20)

| #   | Rule                                                        | Analyzer                                    | Fix  |
| --- | ----------------------------------------------------------- | ------------------------------------------- | ---- |
| 4   | E128167 `TODO`, `FIXME`, or `HACK` comment with no tracker ref | `Style/UntrackedTodoAnalyzer.cs`            | None |
| 5   | E128168 Test `Skip` with no reason or tracker ref              | `Testing/UntrackedSkipAnalyzer.cs`          | None |
| 6   | E128169 `[Obsolete]` with no message                           | `Style/ObsoleteWithoutMessageAnalyzer.cs`   | None |

The option `e128_tracked_ref_pattern` holds the regex for a tracker reference.
The default accepts `AB#123` and `#123`. Sonar S1135 reports every `TODO`. E128167
stays silent when the comment carries a reference.

## Phase 4: Unchecked Assumption (practice #16)

| #   | Rule                                                | Analyzer                                  | Fix  |
| --- | --------------------------------------------------- | ----------------------------------------- | ---- |
| 7   | E128170 `Split(...)[n]` with no length guard        | `Reliability/UnguardedSplitIndexAnalyzer.cs` | None |

Skip `First()`, `Single()`, and the dictionary indexer until review finds a
defect. Each shape has a high false positive rate without data-flow from an
external source. The discarded `Stream.Read` count stays in
[analyzer-candidates.md](../../lode/dotnet/analyzer-candidates.md).

## Phase 5: Test Hygiene And Design (practices #8, #10, #15)

| #   | Rule                                                              | Analyzer                                         | Fix  |
| --- | ----------------------------------------------------------------- | ------------------------------------------------ | ---- |
| 8   | E128171 Committed `Assert.Fail` RED stub                          | `Testing/CommittedRedStubAnalyzer.cs`            | None |
| 9   | E128172 `try`/`catch` swallows an assertion failure               | `Testing/SwallowedAssertionAnalyzer.cs`          | None |
| 10  | E128173 `[SuppressMessage]` or `[ExcludeFromCodeCoverage]` with no justification | `Style/UnjustifiedSuppressionAttributeAnalyzer.cs` | None |
| 11  | E128174 Constructor past the `e128_max_ctor_dependencies` ceiling | `Design/ConstructorDependencyCeilingAnalyzer.cs` | None |

## Script: Diff Size Gate (practices #5, #7)

Add `--max-lines N` to `scripts/diff.sh`. The flag warns when a branch
changes more than N lines. Default 400. Reuse the classifier in
`scripts/internal/mechanical-diff.sh` to report mechanical and substantive lines
apart.

## Gates For Each Phase

```bash
scripts/test.sh <AnalyzerClassName>
scripts/readme-table-diff.sh --analyzer
scripts/internal/analyzer-release-check.sh
scripts/check.sh
```

## See Also

- [tasks](open-work-tasks.md)
- [analyzer checklist](../../lode/dotnet/analyzers/new-analyzer-checklist.md)
- [proof-test sweep plan](../analyzers-2026-11-04/analyzers-2026-11-04-plan.md)
