# Async Fire-And-Forget Candidates
*Updated: 2026-11-11T15:39:59Z*

Open candidates taken from the rules that the `AsyncAwaitBestPractices` library
encodes by convention. Each row names the library feature that implies the
rule. Coverage claims against the installed rule sets are marked probe pending
until a build probe confirms them. The probe method lives in
[redundant-rules.md](analyzers/redundant-rules.md).

Existing owners of the async shapes stay out of this list. E128007 covers an
`async void` method. E128008 covers a blocking wait. CA2007 covers
`ConfigureAwait` polarity. MA0134 covers `Select(async ...)`.

## Candidate 1: Discarded awaitable outside an async method

The library exists to make an unobserved task explicit. `SafeFireAndForget`
observes the exception and logs it. A call that drops the returned task with no
discard and no observer loses every exception from that task.

```csharp
// Reported: the task is dropped and nothing observes a fault.
SendEmailAsync(user);

// Silent: the discard states the intent, and the analyzer asks for a handler.
_ = SendEmailAsync(user);
```

The check is a statement whose expression is an invocation that returns
`Task`, `Task<T>`, or `ValueTask`. Exclusions:

- The enclosing method is `async`, where CS4014 already reports.
- A discard assignment, an assignment, a `return`, or a `yield`.
- A call inside `Task.WhenAll`, `Task.WhenAny`, or a local function body.
- An expression body of a lambda that a factory method consumes.

Coverage: CS4014 fires in an `async` method only. CA2012 covers the multiple
read of a `ValueTask`, not the drop. A probe is pending for VSTHRD110.

Noise: low. Fix: replace the statement with `_ = ` and add a logging
continuation. The fix stays a suggestion because the handler policy is local.

## Candidate 2: `async` lambda converted to a void-returning delegate

An `async` lambda assigned to `Action`, `EventHandler`, or a custom `void`
delegate compiles to `async void`. The exception escapes to the synchronization
context, and no caller can observe it. This is the exact defect that
`AsyncCommand` exists to remove.

```csharp
// Reported: the lambda converts to Action, so the body is async void.
button.Click += async (_, _) => await SaveAsync();
```

The check is an anonymous function with the `async` modifier whose converted
type has a `void` return. The converted type comes from
`SemanticModel.GetTypeInfo`. Exclusions:

- A delegate type that returns `Task` or `ValueTask`.
- A delegate parameter that a method marks to accept `async void`.

Coverage: E128007 registers `MethodDeclaration` only, so it never sees a
lambda. A probe is pending for VSTHRD100 and S3168 on the lambda shape.

Noise: low. Fix: none. The repair depends on the consuming API.

## Candidate 3: Suppressed-throw `await` with no fault check

`ConfigureAwaitOptions.SuppressThrowing` drops the exception from the awaited
operation. The library README states that an `onException` handler never runs in
that state. The same holds for any handler that the caller attaches.

```csharp
// Reported: the await cannot throw, and nothing reads IsFaulted.
await operation.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
```

The check is an `await` over a `ConfigureAwait` call whose argument carries the
`SuppressThrowing` flag, with no later read of `IsFaulted` or `IsCanceled` on
the same receiver inside the block. A statement-level `ConfigureAwait` on a
`Task` argument value also counts.

Coverage: no E128 rule names this API. A probe is pending for CA and Meziantou.

Noise: low. The flag is rare, and the pairing is the invariant. Fix: none.

## Candidate 4: Strong subscription to an injected dependency event

`WeakEventManager` exists because a strong subscriber holds the publisher
alive. A subscriber that subscribes to a constructor-injected dependency and
never removes the handler leaks its own instance when it dies first.

```csharp
// Reported: the field is injected, the type owns no removal, and it has no Dispose.
_cache.Evicted += OnEvicted;
```

The check runs in a symbol start action. It collects `+=` on an event whose
receiver resolves to a constructor-assigned field. It reports when the type
carries no `-=` for that event and implements neither `IDisposable` nor
`IAsyncDisposable`.

Coverage: E128 `EventSubscriptionLeakAnalyzer` collects a static event and an
own-instance event. A dependency event on a field stays open.

Noise: medium. Fix: none. The repair either implements `Dispose` or moves the
subscription to a weak manager.

## Rejected Shapes

These shapes come from the same library and already have an owner.

- `.Result` and `.Wait()`: E128008.
- `async void` method: E128007.
- `ConfigureAwait(false)` polarity: CA2007.
- A discarded `Task` inside an `async` method: CS4014.
- A blocking call inside `lock`: a Tier 2 candidate in
  [analyzer-candidates.md](analyzer-candidates.md).
- `shouldAlwaysRethrowException` and the default exception handler: library
  API specific, with no general code shape.

## Next Steps

Run one probe project against candidates 1 to 3 first. Each candidate needs one
positive snippet and one negative snippet per exclusion. Record the result here
and in [redundant-rules.md](analyzers/redundant-rules.md). The next free
diagnostic id follows the last assigned id in
`src/E128.Analyzers/AnalyzerReleases.Unshipped.md`.

## Related

- [analyzer-candidates.md](analyzer-candidates.md): the candidate catalog
- [analyzer-candidates-external-sweep.md](analyzer-candidates-external-sweep.md):
  the closed external sweep
- [rule-details-reliability-concurrency.md](analyzers/rule-details-reliability-concurrency.md):
  the shipped async rules
- [new-analyzer-checklist.md](analyzers/new-analyzer-checklist.md): the
  pre-flight checks for a new rule
