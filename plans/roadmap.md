# Roadmap
*Updated: 2026-11-18T00:19:04Z*

Unplanned work awaiting a plan.

Each entry carries the shape, the register score, and the code fix. The open
register lives in [index.md](../lode/dotnet/analyzer-candidates/index.md), and
the read that found these shapes lives in
[guidance-reads.md](../lode/dotnet/analyzer-candidates/guidance-reads.md).

## Active

No unplanned work waits for a plan.

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
