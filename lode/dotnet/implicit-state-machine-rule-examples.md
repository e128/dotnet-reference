# Implicit State Machine Rule Examples
*Updated: 2026-11-11T03:39:00Z*

These examples show the E128218 evidence rules. The design detail lives in
[implicit-state-machine-rule-design.md](implicit-state-machine-rule-design.md).
The concept lives in
[implicit-state-machine-rule.md](implicit-state-machine-rule.md).

## Positive: Fields

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

## Positive: Auto-Properties

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

## Positive: Forwarding Accessor Pair

```csharp
sealed class Player
{
    private bool _isRunning;
    private bool _isPaused;
    private bool _isStopped;

    public bool IsRunning { get => _isRunning; set => _isRunning = value; }
    public bool IsPaused { get => _isPaused; set => _isPaused = value; }
    public bool IsStopped { get => _isStopped; set => _isStopped = value; }

    public void Start() { IsRunning = true; IsStopped = false; }
    public void Pause() { IsRunning = false; IsPaused = true; }
}
```

The rule reports the type. The field is the canonical member, so each pair takes
one cluster slot.

## Negative: Reset Only

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

## Negative: External Property Writes

```csharp
sealed class Options
{
    public bool UseCache { get; set; }
    public bool UseRetry { get; set; }
    public bool UseCompression { get; set; }
}

sealed class Loader
{
    public void Configure(Options options)
    {
        options.UseCache = true;
        options.UseRetry = false;
        options.UseCompression = true;
    }
}
```

The type has no internal write, so the internal-write gate keeps it silent.

## Negative: Single Setup Body With The Option Set

```csharp
sealed class Config
{
    private bool _useCache;
    private bool _useRetry;
    private bool _useCompression;

    public void Configure(bool cache, bool retry, bool compression)
    {
        _useCache = cache;
        _useRetry = retry;
        _useCompression = compression;
    }
}
```

The type reports by default. With `e128_state_min_transition_bodies = 2`, the
single setup body is below the minimum and the type stays silent.

## Related

- [implicit-state-machine-rule.md](implicit-state-machine-rule.md): concept and
  contract
- [implicit-state-machine-rule-design.md](implicit-state-machine-rule-design.md):
  member rules, evidence, and test coverage
