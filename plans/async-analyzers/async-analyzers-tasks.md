# Async Fire-And-Forget Tasks
*Updated: 2026-11-11T16:24:00Z*

## Phase 0: Probe

- [x] Create `.claude/tmp/async-probe/` with one csproj that references the installed analyzer packages
- [x] Add the step 1 snippets: statement drop, `ValueTask` drop, drop inside `async`
- [x] Add the step 2 snippets: `async` lambda to `Action`, to `EventHandler`, and to `Func<Task>`
- [x] Add the step 3 snippet: `SuppressThrowing` await
- [x] Add the step 4 snippets: dependency event on a disposable and a non-disposable subscriber
- [x] Record which rule IDs fire
- [x] Write the result table into `lode/dotnet/analyzers/redundant-rules.md`
- [x] Drop every candidate that a shipped rule already reports

## Phase 1: E128223 (cancelled)

- [x] Cancelled. MA0134 and VSTHRD110 report the discarded awaitable.

## Phase 2: E128224 (cancelled)

- [x] Cancelled. AsyncFixer03, MA0147, and VSTHRD101 report the `async` lambda.

## Phase 3: E128225 (cancelled)

- [x] Cancelled on value. The API has no use in `src/`, and the flag states intent.

## Phase 4: E128176 extension (cancelled)

- [x] Cancelled. The open case is a non-disposable subscriber, which is a long-lived type.

## Phase 5: Tooling and docs

- [x] Let `scripts/build.sh --project` take a `.csproj` path
- [x] Print warning lines when `--warnings` is set
- [x] `scripts/lint-shell.sh` passes on both edited scripts
- [x] Run `scripts/lode-check.sh` on every edited lode file
- [x] Close the candidate entries in `lode/dotnet/analyzer-candidates-async.md`
- [x] Update the `lode-map.md` hook

## Phase 6: Gate

- [x] Probe build runs through `scripts/build.sh`
- [x] No analyzer suppression is added
- [ ] Run `scripts/check.sh` before commit
