# Async Fire-And-Forget Tasks
*Updated: 2026-11-11T15:41:28Z*

## Phase 0: Probe

- [ ] Create `.claude/tmp/async-probe/` with one csproj that references the installed analyzer packages
- [ ] Add the step 1 snippets: statement drop, `_ =` drop, drop inside `async`, drop inside `ConfigureAwait` chain
- [ ] Add the step 2 snippets: `async` lambda to `Action`, `async` lambda to `Func<Task>`, `async` handler lambda
- [ ] Add the step 3 snippet: `SuppressThrowing` await with and without an `IsFaulted` read
- [ ] Record which rule IDs fire, including `VSTHRD100`, `VSTHRD110`, `S3168`, `CA2012`, `MA0040`, and the compiler
- [ ] Write the result table into `lode/dotnet/analyzers/redundant-rules.md`
- [ ] Drop any candidate that a shipped rule already reports, and note the drop in the candidates file

## Phase 1: E128223

- [ ] Write the failing tests in `tests/E128.Analyzers.Tests/DiscardedAwaitableAnalyzerTests.cs`
- [ ] Add `src/E128.Analyzers/Reliability/DiscardedAwaitableAnalyzer.cs`
- [ ] Resolve the three awaitable types once in `RegisterCompilationStartAction`
- [ ] Exclude the `async` enclosing member, the discard, an assignment, a return, a yield, and a helper consumer
- [ ] Run `scripts/test.sh DiscardedAwaitableAnalyzerTests`

## Phase 2: E128224

- [ ] Write the failing tests in `tests/E128.Analyzers.Tests/AsyncLambdaToVoidDelegateAnalyzerTests.cs`
- [ ] Add `src/E128.Analyzers/Design/AsyncLambdaToVoidDelegateAnalyzer.cs`
- [ ] Read the converted type through `GetTypeInfo`, and compare the return type by symbol
- [ ] Exclude a delegate that returns `Task`, `Task<T>`, or `ValueTask`
- [ ] Run that test class

## Phase 3: E128225

- [ ] Write the failing tests in `tests/E128.Analyzers.Tests/SuppressedThrowAwaitAnalyzerTests.cs`
- [ ] Add `src/E128.Analyzers/Reliability/SuppressedThrowAwaitAnalyzer.cs`
- [ ] Match the flag through the field symbol on `ConfigureAwaitOptions`, not by name text
- [ ] Scan the enclosing block for a later `IsFaulted` or `IsCanceled` read on the same receiver
- [ ] Run that test class

## Phase 4: E128176 extension

- [ ] Add the dependency-event cases to `EventSubscriptionLeakAnalyzerTests.cs`
- [ ] Add the second descriptor and the constructor-assigned field collection
- [ ] Run `scripts/test.sh EventSubscriptionLeakAnalyzerTests` and confirm the existing cases stay silent

## Phase 5: Docs and release

- [ ] Add each rule to the table in `src/E128.Analyzers/README.md`
- [ ] Add each rule to `src/E128.Analyzers/AnalyzerReleases.Unshipped.md`
- [ ] Run `scripts/readme-table-diff.sh --analyzer`
- [ ] Update `lode/dotnet/analyzers/rule-details-reliability-concurrency.md` and `rule-details.md`
- [ ] Close the candidate entries in `lode/dotnet/analyzer-candidates-async.md`
- [ ] Run `scripts/check.sh` and `scripts/lode-check.sh --changed`

## Gate

- [ ] `scripts/check.sh` passes
- [ ] The test count in each run output is not zero
- [ ] No analyzer suppression is added
