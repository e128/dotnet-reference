# Implicit State Machine Rule
*Updated: 2026-11-12T01:47:01Z*

E128218 flags a class or a mutable struct that uses several mutable `bool`
members as one state. The members change together, so their valid combinations
are constrained in scattered code. The rule reports a cluster, not a defect. A
review decides the remedy.

The implementation lives in `src/E128.Analyzers/Design/ImplicitStateMachineAnalyzer.cs`.
The tests live in `tests/E128.Analyzers.Tests/ImplicitStateMachineAnalyzerTests.cs`.
The design detail lives in
[implicit-state-machine-rule-design.md](implicit-state-machine-rule-design.md).

## What The Rule Detects

The rule sees four facts:

- the declarations of instance `bool` fields and auto-properties
- the declaration of a property pair that forwards to one field
- the writes to those members, including the constant value written
- the conditions that read several members together

A pair of independent flags looks the same as a state variable at one call
site. The rule therefore ships at `Info`, and the repository sets it to
`error`.

## Detector Families

| Shape | Signal | Noise | Status |
| --- | --- | --- | --- |
| Boolean member cluster | Several mutable `bool` members are co-written or read in one condition | Medium | Shipped as E128218 |
| String status field | A `string` field is compared with several literals | Low | Separate candidate |
| Timestamp status bag | Several nullable timestamp fields stand in for one status | High | Rejected |
| Loop-exit flag | A `bool` exists only to break a loop | Covered by IDE0410 | Rejected |
| Async or iterator state machine | The compiler generates the state machine | Not a source defect | Rejected |

## Rule Contract

| Field | Value |
| --- | --- |
| ID | `E128218` |
| Title | Boolean flags encode one state |
| Category | `E128-Design` |
| Severity | `Info` |
| Code fix | None |
| Option | `e128_min_state_flags`, default `3`, minimum `2` |
| Option | `e128_state_min_transition_bodies`, default `1`, minimum `1` |

The rule reads both options per analyzed type from its declaring file, so a
project `.editorconfig` applies.

The rule has no code fix. The state names and the legal transitions are domain
decisions.

## Evidence

### Co-write

A method writes two or more cluster members with different constant values, or
with one unknown value. The mixed value is the manual form of an enum
assignment.

```csharp
public void Pause()
{
    _isRunning = false;
    _isPaused = true;
}
```

A reset method that writes every member to `false` does not count. That shape is
cleanup, not a transition.

A write from another type counts when it names a candidate through a setter.

```csharp
public void Pause(Machine machine)
{
    machine.IsRunning = false;
    machine.IsPaused = true;
}
```

The owning type must have at least one transition in its own body. The gate
keeps a configuration object out of the report.

### Co-read

A condition reads two or more cluster members, and at least one of them is
negated. The rule requires a cluster one member larger than the configured
minimum. A positive-positive read, such as `_useCache && _useRetry`, is usually
independent switches.

```csharp
if (_isRunning && !_isPaused)
{
    Render();
}
```

## Scope And Exclusions

The rule analyzes classes and mutable structs. It skips records, `readonly`
structs, interfaces, static classes, and types outside the current compilation.

A member is a candidate when it is an instance `bool` field, or a `bool`
auto-property with a setter. A field is excluded when it is `static`, `const`,
`readonly`, implicitly declared, or `[Obsolete]`.

A property pair with a getter and a setter that only read and write one field
shares that field as its canonical member. The pair counts once. A computed
property is outside the scope.

A candidate joins the cluster only when its transitions are not one-way. A
member written only to `true`, or only to `false`, is ignored. An unknown write,
such as `_flag = !_flag`, keeps the member.

A write in a constructor, a static constructor, or a destructor is not a
transition. A write inside a property accessor is storage, not a transition. A
write through `Interlocked.Exchange` or `Volatile.Write` is a transition. A
write in a lambda or a local function is grouped with that body, not with the
enclosing method.

## Report Shape

- Location: the type identifier.
- Additional locations: the first mixed co-write and the first co-read.
- Message: "`{0}` uses {1} boolean flags as one state: {2}. Use one enum and one
  transition method."

## False Positive Budget

| Shape | Why it fires | Mitigation |
| --- | --- | --- |
| Reset method clears several fields | One body writes several fields | Require a mixed value |
| Disposal flag plus state fields | Dispose writes both | Ignore one-way fields |
| Options or DTO with public setters | External writers form a cluster | Require at least one internal write |
| One setup method assigns several flags | One body holds the only mixed write | `e128_state_min_transition_bodies` set to `2` |
| Feature switches read together | Four toggled members read in one condition | Higher co-read threshold and a negated read |
| Cache validity pair | A reset writes both | `Info` severity |

The repository sets `dotnet_diagnostic.E128218.severity = error` in
`.globalconfig`, so a cluster report fails the build. A consumer keeps the
default `Info`.

## Duplicate Owners

- IDE0410 covers the loop-exit flag in the .NET 11 analyzer set.
- E128048 covers an `if`/`else-if` chain on enum values.
- Sonar S1125 covers a boolean literal comparison.
- No installed rule in the package manifest targets a field cluster.

## Known Gaps

- A computed property is outside the scope.
- A write from another assembly is outside the scope.
- A write through a ref alias or an `Unsafe` API is missed.
- A conditional write through `Interlocked.CompareExchange` is missed.
- A two-member cluster needs `e128_min_state_flags = 2`.
- A cluster of one transition body needs `e128_state_min_transition_bodies`
  left at its default.

## Related

- [implicit-state-machine-rule-design.md](implicit-state-machine-rule-design.md):
  field rules, evidence, registration, and test coverage
- [implicit-state-machine-rule-examples.md](implicit-state-machine-rule-examples.md):
  positive and negative snippets
- [analyzer-candidates/catalog.md](analyzer-candidates/catalog.md): candidate ledger and ID
  allocation
- [analyzers/new-analyzer-checklist.md](analyzers/new-analyzer-checklist.md):
  registration, symbol matching, and netstandard2.0 constraints
