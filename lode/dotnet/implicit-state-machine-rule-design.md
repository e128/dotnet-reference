# Implicit State Machine Rule Design
*Updated: 2026-11-07T19:46:15Z*

This file details the E128218 rule in
[implicit-state-machine-rule.md](implicit-state-machine-rule.md).
It covers the member rules, the evidence, the registration shape, and the test
coverage.

## Candidate Members

A member is a candidate when all of these hold:

- it is an instance field or auto-property of the analyzed class
- its type is exactly `bool`, not `bool?`
- a field is not `const`, `readonly`, or implicitly declared
- a property has a setter that is not `init`-only
- a property has no accessor body and no expression body
- it is not marked `[Obsolete]`

```csharp
sealed class Download
{
    private bool _isRunning;                  // candidate
    private readonly bool _trace;             // removed, cannot change
    public bool IsPaused { get; set; }        // candidate
    public bool IsReady { get => _isRunning; } // removed, no setter
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
start.RegisterCompilationEndAction(ctx => Report(ctx, map, minFlags));
```

Resolve members with `SymbolEqualityComparer.Default`. Never match a member by
name. Read the option once per compilation. A loop condition comes from
`IWhileLoopOperation` or `IForLoopOperation`, because `ILoopOperation` has no
`Condition` member. A write through `Interlocked.Exchange` or `Volatile.Write`
is a transition write.

## Worked Examples

### Positive: fields

```csharp
sealed class Player
{
    private bool _isIdle;
    private bool _isRunning;
    private bool _isPaused;

    public void Start()  { _isIdle = false; _isRunning = true; }
    public void Stop()   { _isRunning = false; _isIdle = true; }
    public void Pause()  { _isRunning = false; _isPaused = true; }
    public void Resume() { _isPaused = false; _isRunning = true; }
}
```

The rule reports the type. The remedy is one `PlayerState` enum with `Idle`,
`Running`, and `Paused`, plus one transition method.

### Positive: auto-properties

```csharp
sealed class UiState
{
    public bool IsLoading { get; set; }
    public bool HasError { get; set; }
    public bool IsComplete { get; set; }

    public void Load()
    {
        IsLoading = true;
        HasError = false;
        IsComplete = false;
    }

    public void Fail()
    {
        IsLoading = false;
        HasError = true;
        IsComplete = false;
    }

    public void Complete()
    {
        IsLoading = false;
        HasError = false;
        IsComplete = true;
    }
}
```

The rule reports the type. The three auto-properties are two-valued, and each
method mixes the values.

### Negative: reset only

```csharp
sealed class Flags
{
    private bool _a;
    private bool _b;
    private bool _c;

    public void ToggleA() { _a = !_a; }
    public void ToggleB() { _b = !_b; }
    public void ToggleC() { _c = !_c; }

    public void Reset()
    {
        _a = false;
        _b = false;
        _c = false;
    }
}
```

The members are two-valued, but `Reset` writes the same value to all of them.
The type does not report.

### Negative: external property writes

```csharp
sealed class Options
{
    public bool UseCache { get; set; }
    public bool UseRetry { get; set; }
    public bool UseCompression { get; set; }
}
```

The type has no internal write, so no cluster forms.

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
| Explicit property accessor bodies | Silent |
| `Volatile.Write` co-write | Report |
| `Interlocked.Exchange` co-write | Report |
| `readonly`, `const`, and `static` fields | Silent |
| `bool?` members | Silent |
| Record type | Silent |
| Struct type | Silent |
| Partial type | Report |
| Generic type | Report |
| Constructor-only writes | Silent |
| Constructor then transitions | Report |
| Lambda writes in separate bodies | Silent |
| Lambda mixed co-write | Report |
| Local function mixed co-write | Report |
| `[Obsolete]` field | Silent |
| Field initializers only | Silent |
| Labeled corpus: three state machines and three near misses | 3 report, 3 silent |

Every test carries `[Trait("Category", "CI")]`. The rule has no code fix, so the
fixed text equals the input text.

## Known Gaps

- A property with accessor bodies is outside the scope.
- A transition in another type through a public setter is outside the scope.
- A write through a ref alias or an `Unsafe` API is missed.
- A conditional write through `Interlocked.CompareExchange` is missed.
- A mutable struct with several `bool` members is outside the scope.
- A two-member cluster needs `e128_min_state_flags = 2`.

## Related

- [implicit-state-machine-rule.md](implicit-state-machine-rule.md): concept,
  detector families, contract, and false positive budget
- [analyzer-candidates.md](analyzer-candidates.md): candidate ledger and ID
  allocation
- [analyzers/rule-details.md](analyzers/rule-details.md): detection shape for a
  shipped rule
