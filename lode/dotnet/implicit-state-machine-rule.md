# Implicit State Machine Rule
*Updated: 2026-11-07T19:42:49Z*

E128218 flags a class that uses several mutable `bool` fields as one state.
The fields change together, so their valid combinations are constrained in
scattered code. The rule reports a cluster, not a defect. A review decides the
remedy.

The implementation lives in `src/E128.Analyzers/Design/ImplicitStateMachineAnalyzer.cs`.
The tests live in `tests/E128.Analyzers.Tests/ImplicitStateMachineAnalyzerTests.cs`.
The design detail lives in
[implicit-state-machine-rule-design.md](implicit-state-machine-rule-design.md).

## What The Rule Detects

The rule sees three facts:

- the declarations of instance `bool` fields and auto-properties
- the writes to those members, including the constant value written
- the conditions that read several members together

A pair of independent flags looks the same as a state variable at one call
site. The rule therefore stays at `Info`, and the repository sets it to
`suggestion`.

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

The rule analyzes classes only. It skips records, structs, interfaces, static
classes, and types outside the current compilation.

A member is a candidate when it is an instance `bool` field, or a `bool`
auto-property with a setter. A field is excluded when it is `static`, `const`,
`readonly`, implicitly declared, or `[Obsolete]`. A property with accessor
bodies is outside the scope.

A candidate joins the cluster only when its transitions are not one-way. A
member written only to `true`, or only to `false`, is ignored. An unknown write,
such as `_flag = !_flag`, keeps the member.

A write in a constructor, a static constructor, or a destructor is not a
transition. A write through `Interlocked.Exchange` or `Volatile.Write` is a
transition. A write in a lambda or a local function is grouped with that body,
not with the enclosing method.

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
| Options or DTO with public setters | External writers form a cluster | Analyze fields and auto-properties, and require internal writes |
| Feature switches read together | Four toggled members read in one condition | Higher co-read threshold and a negated read |
| Cache validity pair | A reset writes both | `Info` severity |

The repository sets `dotnet_diagnostic.E128218.severity = suggestion` in
`.globalconfig`. A consumer keeps the default `Info`.

## Duplicate Owners

- IDE0410 covers the loop-exit flag in the .NET 11 analyzer set.
- E128048 covers an `if`/`else-if` chain on enum values.
- Sonar S1125 covers a boolean literal comparison.
- No installed rule in the package manifest targets a field cluster.

## Known Gaps

- A property with accessor bodies is outside the scope.
- A transition in another type through a public setter is outside the scope.
- A write through a ref alias or an `Unsafe` API is missed.
- A conditional write through `Interlocked.CompareExchange` is missed.
- A mutable struct with several `bool` members is outside the scope.
- A two-member cluster needs `e128_min_state_flags = 2`.

## Related

- [implicit-state-machine-rule-design.md](implicit-state-machine-rule-design.md):
  field rules, evidence, registration, examples, and test coverage
- [analyzer-candidates.md](analyzer-candidates.md): candidate ledger and ID
  allocation
- [analyzers/new-analyzer-checklist.md](analyzers/new-analyzer-checklist.md):
  registration, symbol matching, and netstandard2.0 constraints
