# Implicit State Machine Rule Design
*Updated: 2026-11-11T03:39:00Z*

This file details the E128218 rule in
[implicit-state-machine-rule.md](implicit-state-machine-rule.md).
It covers the member rules, the evidence, the registration shape, and the test
coverage.

## Candidate Members

A member is a candidate when all of these hold:

- it is an instance field or auto-property of the analyzed class or struct
- its type is exactly `bool`, not `bool?`
- a field is not `const`, `readonly`, or implicitly declared
- a property has a setter that is not `init`-only
- a property has no accessor body and no expression body
- it is not marked `[Obsolete]`

A property pair with an explicit body joins through its field. The getter must
return one field, and the setter must assign that same field from `value`. The
field is the canonical member, so the pair occupies one cluster slot.

```csharp
sealed class Download
{
    private bool _isRunning;                  // candidate
    private readonly bool _trace;             // removed, cannot change
    public bool IsPaused { get; set; }        // candidate
    public bool IsReady { get => _isRunning; } // removed, no setter
    public bool IsBusy { get => _isRunning; set => _isRunning = value; } // maps to _isRunning
}
```

A candidate joins the cluster only when its transitions are not one-way. The
rule tracks the constant value of each write. A member written only to `true`,
or only to `false`, is removed. An unknown write keeps the member.

## Evidence

### Co-write

A body writes two or more cluster members. The writes must not all carry the
same constant value. One unknown value counts as mixed.

```csharp
public void Pause()
{
    _isRunning = false;
    _isPaused = true;
}
```

The mixed value separates a transition from a reset. A reset writes every
member to `false` and does not report.

### Co-read

A condition reads two or more cluster members, and at least one of them is
negated. The rule requires a cluster one member larger than the configured
minimum, because a read alone is weak evidence. A positive-positive read is
usually independent switches.

```csharp
if (_isRunning && !_isPaused)
{
    Render();
}
```

Negation covers `!`, `== false`, `!= true`, `is false`, and `is not true`.

### Cross-type write

A body in another type can write a candidate through a setter. That write joins
the evidence of the owning type, and it keeps the writer as its body key. The
owning type must have at least one transition in its own body. The gate keeps a
configuration object out of the report.

A write inside a property accessor is storage, not a transition. The rule skips
it, so a forwarding pair is counted at its use sites.

### Transition bodies

The cluster reports when the count of bodies that write a cluster member meets
`e128_state_min_transition_bodies`. The default is one. A value of two requires
two separate transition bodies.

### Deferred

Mutual exclusion across every write is strong evidence. It needs a whole-type
data-flow pass and a strategy for writes outside the type. The rule does not
implement it.

## Registration

The rule uses the two-pass shape from `SealedByDefaultAnalyzer`. The first pass
records evidence, and the second pass reports at compilation end.

```csharp
start.RegisterOperationAction(ctx => RecordWrite(ctx, map, obsolete), OperationKind.SimpleAssignment, OperationKind.CompoundAssignment);
start.RegisterOperationAction(ctx => RecordConditionRead(ctx, map, obsolete), OperationKind.Conditional, OperationKind.Loop);
start.RegisterOperationAction(ctx => RecordInvocation(ctx, map, obsolete, interlocked, volatile), OperationKind.Invocation);
start.RegisterCompilationEndAction(ctx => Report(ctx, map));
```

Resolve members with `SymbolEqualityComparer.Default`. Never match a member by
name, with one exception. The forwarding detector reads the field name from the
accessor syntax and finds that name in the declaring type. A diagnostic analyzer
must not call `Compilation.GetSemanticModel`, and the name is unique inside one
type.

Read both options per analyzed type from its declaring file, so a project
`.editorconfig` applies. A loop condition comes from `IWhileLoopOperation` or
`IForLoopOperation`, because `ILoopOperation` has no `Condition` member. A
write through `Interlocked.Exchange` or `Volatile.Write` is a transition write.

## Worked Examples

The examples live in
[implicit-state-machine-rule-examples.md](implicit-state-machine-rule-examples.md).

## Test Coverage

The test file covers these cases:

| Case | Expected |
| --- | --- |
| Three fields, mixed co-writes | Report |
| Three fields, no evidence | Silent |
| Three fields, separate toggles | Silent |
| Four fields, negated co-read in an `if` | Report |
| Four fields, positive co-read in an `if` | Silent |
| Five fields, negated co-read in a `while` | Report |
| Three fields, negated co-read below the threshold | Silent |
| Equality form of a negated co-read | Report |
| `is false` pattern of a negated co-read | Report |
| Two fields, default minimum | Silent |
| Two fields, minimum set to two | Report |
| Reset writes one value to all | Silent |
| Disposal flag plus two state members | Silent |
| Auto-property state | Report |
| Property reset only | Silent |
| Init-only, get-only, and static properties | Silent |
| Abstract properties | Silent |
| External property writes | Silent |
| Forwarding accessor pair | Report |
| Computed property | Silent |
| Forwarding pair, minimum set to two | Report |
| Forwarding pair, default minimum | Silent |
| Cross-type write through a setter | Report |
| Cross-type write with no internal transition | Silent |
| `Volatile.Write` co-write | Report |
| `Interlocked.Exchange` co-write | Report |
| `readonly`, `const`, and `static` fields | Silent |
| `bool?` members | Silent |
| Record type | Silent |
| Mutable struct | Report |
| `readonly` struct | Silent |
| `record struct` | Silent |
| Partial type | Report |
| Generic type | Report |
| Constructor-only writes | Silent |
| Constructor then transitions | Report |
| Lambda writes in separate bodies | Silent |
| Lambda mixed co-write | Report |
| Local function mixed co-write | Report |
| `[Obsolete]` field | Silent |
| Field initializers only | Silent |
| Two transition bodies, option set to two | Report |
| One transition body, default | Report |
| One transition body, option set to two | Silent |
| Labeled corpus: state machines and near misses | Every labeled type matches |

Every test carries `[Trait("Category", "CI")]`. The rule has no code fix, so the
fixed text equals the input text.

## Known Gaps

- A computed property is outside the scope.
- A write from another assembly is outside the scope.
- A write through a ref alias or an `Unsafe` API is missed.
- A conditional write through `Interlocked.CompareExchange` is missed.
- A two-member cluster needs `e128_min_state_flags = 2`.
- A cluster of one transition body needs `e128_state_min_transition_bodies`
  left at its default.

## Related

- [implicit-state-machine-rule.md](implicit-state-machine-rule.md): concept,
  detector families, contract, and false positive budget
- [implicit-state-machine-rule-examples.md](implicit-state-machine-rule-examples.md):
  positive and negative snippets
- [analyzer-candidates.md](analyzer-candidates.md): candidate ledger and ID
  allocation
- [analyzers/rule-details.md](analyzers/rule-details.md): detection shape for a
  shipped rule
