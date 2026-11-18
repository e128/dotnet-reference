# Roadmap
*Updated: 2026-11-18T12:18:45Z*

Unplanned work awaiting a plan.

Each entry carries the shape, the register score, and the code fix. The open
register lives in [index.md](../lode/dotnet/analyzer-candidates/index.md), and
the read that found these shapes lives in
[guidance-reads.md](../lode/dotnet/analyzer-candidates/guidance-reads.md).

## Active

### `dynamic` Keyword Ban
- **Added:** 2026-11-18T12:18:00Z
- **Register:** score 4/5, status `new`, RECOMMEND.
- **Source:** [slopwatch-sweep.md](../lode/dotnet/analyzer-candidates/slopwatch-sweep.md)
  (rule SW021).
- **Shape:** an identifier named `dynamic` in a type position. The keyword
  defeats compile-time checking, trimming, and AOT.

```csharp
// Report
dynamic payload = Parse(body);
return payload.Value;
```

- **Detection:** `SyntaxFacts.IsInNamespaceOrTypeContext` answers the type
  position. Exclude an attribute argument and a using directive with no alias. A
  semantic model lets the rule skip a real type named `dynamic`, which the
  slopwatch source scanner cannot do.
- **Fix scope:** no code fix. A replacement type carries application intent.
- **Evidence:** zero uses of the keyword in `src/` (as of 2026-11-18), so the
  rule ships clean.
- **Trigger:** ready for a plan.

## Waiting

### Parallel For Over a Per-Item Body
- **Added:** 2026-11-17T23:07:20Z
- **Register:** Tier 2, waits on a real defect.
- **Shape:** `Parallel.For` runs one delegate per item. A range partition runs one delegate per chunk.

```csharp
// Report
Parallel.For(0, items.Count, i => Work(items[i]));

// Fix
Parallel.ForEach(Partitioner.Create(0, items.Count), range =>
{
    for (int i = range.Item1; i < range.Item2; i++)
    {
        Work(items[i]);
    }
});
```

- **Fix scope:** report a body with no loop. Suppress a body that awaits, because the range overload takes a synchronous delegate.
- **Trigger:** A real defect in review, or two occurrences in one commit.

### FileStream Shared Across Reader Threads
- **Added:** 2026-11-17T23:07:20Z
- **Register:** Tier 2, waits on a real defect.
- **Shape:** one `FileStream` position shared by parallel readers. `File.OpenHandle` plus `RandomAccess.Read` reads at an explicit offset.

```csharp
// Report
using FileStream stream = File.OpenRead(path);
Parallel.ForEach(offsets, offset => ReadAt(stream, offset));

// Fix
using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read);
Parallel.ForEach(offsets, offset => ReadAt(handle, offset));
```

- **Fix scope:** report a `FileStream` local captured by a parallel body. The `ReadAt` helper changes to take a `SafeFileHandle` and an offset.
- **Trigger:** A real defect in review. The fix crosses a helper boundary, so it needs a site to prove.

### Readonly Field Hoisted Out of a Loop
- **Added:** 2026-11-17T23:07:20Z
- **Register:** Tier 2, waits on a real defect.
- **Shape:** a loop reads the same `readonly` field by index. A local copy lets the JIT drop the repeated load and the bounds check.

```csharp
// Report
for (int i = 0; i < _items.Length; i++)
{
    total += _items[i];
}

// Fix
int[] items = _items;
for (int i = 0; i < items.Length; i++)
{
    total += items[i];
}
```

- **Fix scope:** report a `readonly` field only. A mutable field can change under a concurrent writer, and the local copy would hide the change.
- **Trigger:** A real defect in review, or a measured gain on a hot path.

### Unconditional Delay In A Test
- **Added:** 2026-11-18T12:18:00Z
- **Register:** Tier 2, score 3.5, waits on a real defect.
- **Source:** [slopwatch-sweep.md](../lode/dotnet/analyzer-candidates/slopwatch-sweep.md)
  (rule SW004).
- **Shape:** a test method waits a fixed time instead of waiting for a signal.

```csharp
// Report
await Task.Delay(500);
Assert.Equal(3, counter.Value);

// Fix
await signal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
```

- **Fix scope:** report `Thread.Sleep`, `SpinWait`, and `Task.Delay` with a
  constant and no token, outside a loop. A delay with a token stays silent.
  E128211 already owns the loop case.
- **Trigger:** A real defect in review, or a flaky test traced to a fixed wait.

### Test Disabled Outside an xUnit `Skip`
- **Added:** 2026-11-18T12:18:00Z
- **Register:** Tier 2, score 3, waits on a real defect.
- **Source:** [slopwatch-sweep.md](../lode/dotnet/analyzer-candidates/slopwatch-sweep.md)
  (rule SW001).
- **Shape:** an `[Ignore]` attribute (NUnit, MSTest) or an `#if false` block
  disables a test method. E128168 reads the xUnit `Skip` argument only.

```csharp
// Report
[Test]
[Ignore("flaky on CI")]
public void WritesTheRegistryValue() { }
```

- **Fix scope:** read a test attribute by symbol for the three frameworks. Apply
  the existing `e128_tracked_ref_pattern` option to an `[Ignore("reason")]`
  argument, so a tracked skip stays silent. Report a method inside `#if false`
  at every site, because that form carries no reason. Read `#else` and `#elif`
  before reporting, as slopwatch learned to do.
- **Trigger:** A real defect in review, or a second test framework in a consumer
  repository.
