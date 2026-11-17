# Roadmap
*Updated: 2026-11-17T23:07:20Z*

Unplanned work awaiting a plan.

Each entry carries the shape, the register score, and the code fix. The open
register lives in [index.md](../lode/dotnet/analyzer-candidates/index.md), and
the read that found these shapes lives in
[guidance-reads.md](../lode/dotnet/analyzer-candidates/guidance-reads.md).

## Active

### Middleware Next Delegate Closure
- **Added:** 2026-11-17T23:07:20Z
- **Register:** 4/5, `new`. Candidate for E128229.
- **Shape:** a `Use` lambda that takes a `Func<Task>` next delegate. That overload allocates one closure per request.

```csharp
// Report
app.Use(async (HttpContext context, Func<Task> next) =>
{
    await next();
});

// Fix
app.Use(async (HttpContext context, RequestDelegate next) =>
{
    await next(context);
});
```

- **Fix scope:** rewrite the parameter type and the call. Report only where `context` stays live and is never reassigned. The `RequestDelegate` overload exists on .NET 7 and later.
- **Trigger:** Plan this entry first. The shape is clear and the fix is a local rewrite.

### Static Files Mapped at Build Time
- **Added:** 2026-11-17T23:07:20Z
- **Register:** 4/5, `new`. Candidate for E128230.
- **Shape:** `UseStaticFiles` sends a file through middleware at request time. `MapStaticAssets` serves a build-time manifest with precompressed variants and ETags.

```csharp
// Report
app.UseStaticFiles();

// Fix
app.MapStaticAssets();
```

- **Fix scope:** report a `UseStaticFiles` call in a project that targets `net9.0` or later. No automatic fix, because the call moves into the endpoint mapping section. A Razor app also changes how it references the assets.
- **Trigger:** Plan after the middleware rule, because the same project shape and TFM check apply.

### Split Result Used Only to Enumerate
- **Added:** 2026-11-17T23:07:20Z
- **Register:** 3.5/5, `new`. Candidate for E128231.
- **Shape:** `string.Split` allocates one array and one string per part. `MemoryExtensions.Split` yields `Range` values over the source string.

```csharp
// Report
foreach (string part in line.Split(','))
{
    total += int.Parse(part);
}

// Fix
foreach (Range range in line.AsSpan().Split(','))
{
    total += int.Parse(line.AsSpan()[range]);
}
```

- **Fix scope:** report only where no code stores the array. The separator is a `char` or a constant string. Rewrite only where every read is a slice, a comparison, or a numeric parse. Report without a fix when the body stores a part as a string.
- **Trigger:** Plan after the two ASP.NET Core rules.

### TensorPrimitives Min and Max Loops
- **Added:** 2026-11-17T23:07:20Z
- **Register:** extension of E128135, no new ID. See [catalog.md](../lode/dotnet/analyzer-candidates/catalog.md).
- **Shape:** E128135 covers `TensorPrimitives.Sum` only. A loop that computes `Min`, `Max`, `Add`, or `Multiply` over one span is the same shape.

```csharp
// Report
double max = double.MinValue;
foreach (double value in values)
{
    if (value > max)
    {
        max = value;
    }
}

// Fix
double max = TensorPrimitives.Max(values);
```

- **Fix scope:** reuse the E128135 detection and code fix. Add the four members to the same member table. Report only a single-span loop with no other side effect.
- **Trigger:** Plan with E128135. The extension touches one analyzer and one code fix.

### List Indexer Loop to a Span
- **Added:** 2026-11-17T23:07:20Z
- **Register:** extension of E128084, no new ID. Guards the open `CollectionsMarshal.AsSpan` Tier 2 row.
- **Shape:** E128084 covers `List.GetRange` only. A `for` loop that indexes a `List<T>` repeats a bounds check per element.

```csharp
// Report
int total = 0;
for (int i = 0; i < items.Count; i++)
{
    total += items[i].Length;
}

// Fix
int total = 0;
foreach (string item in CollectionsMarshal.AsSpan(items))
{
    total += item.Length;
}
```

- **Fix scope:** suppress the report when the loop mutates the list. The span reads stale memory after a mutation, so the fix is wrong for that shape.
- **Trigger:** Plan with E128084. The fix reuses the existing `List.GetRange` rewrite.

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
