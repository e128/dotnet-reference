# Performance Anti-Pattern Catalog (E128-Gap)

Reference for the performance-focused review agent and `--perf` flag on `/review --local`. ~15 patterns NOT enforced by E128 build-time analyzers.

**E128-covered patterns are excluded** — patterns already flagged by the repo's E128 build-time performance analyzers do not belong here; skip them silently.

## Pattern Catalog

### HIGH Severity -- Allocations on Hot Paths / Sync I/O

| # | Pattern | Detection (`rg`) | Why It Matters | Fix |
|---|---------|-------------------|----------------|-----|
| H1 | Chained `.Replace()` | `rg "\.Replace\(.*\.Replace\(.*\.Replace\(" src/ -g "*.cs"` | Each `.Replace()` allocates a new string; 3+ in sequence = 3+ intermediate allocations | Use `StringBuilder` with sequential `.Replace()`, or `Regex.Replace` with alternation |
| H2 | Sync File I/O wrappers | `rg "File\.(ReadAllText\|ReadAllBytes\|WriteAllText\|WriteAllBytes\|ReadAllLines)\b" src/ -g "*.cs"` | Blocks thread pool thread; async variants available since .NET 6 | Use `File.ReadAllTextAsync` / `File.ReadAllBytesAsync` etc. |
| H3 | Stream.ReadByte loop | `rg "\.ReadByte\(\)" src/ -g "*.cs"` | Per-byte syscalls; 1000x slower than buffered reads | Use `Stream.Read(buffer)` or `StreamReader` |

### MEDIUM Severity -- Missing Capacity / Enum Boxing

| # | Pattern | Detection (`rg`) | Why It Matters | Fix |
|---|---------|-------------------|----------------|-----|
| M1 | `new List<T>()` no capacity | `rg "new List<" src/ -g "*.cs" \| rg -v "(ServiceCollectionExtensions\|\.Serialization\.\|\.Dto\.\|\.Models\.\|\.Commands\.\|/Commands/\|/Output/\|/Rendering/\|Registration\|Startup\|Program\.cs)"` | Doubling reallocations when size is known. Only flag when file is in a hot-path namespace (Indexing/, Chunking/, Pipeline/, Processing/) | `new List<T>(count)` or `.ToArray()` |
| M2 | `new MemoryStream()` no capacity | `rg "new MemoryStream\(\)" src/ -g "*.cs"` | Default 256-byte buffer doubles repeatedly | `new MemoryStream(expectedSize)` |
| M3 | `Enum.HasFlag` boxing | `rg "\.HasFlag\(" src/ -g "*.cs"` | Boxes both operands on older runtimes; .NET 7+ JIT eliminates boxing but pattern is still noisy | Bitwise: `(flags & Flag) != 0` |
| M4 | LINQ `.Count()` vs property | `rg "\.Count\(\)" src/ -g "*.cs" \| rg -v "(\.GroupBy\|IGrouping\|groups?\.\w*Count\(\)\|g\.Count\(\))"` | Extension method enumerates entire collection; `.Count` or `.Length` property is O(1) | Use `.Count` property or `.Length` |
| M5 | `Directory.GetFiles` | `rg "Directory\.GetFiles\b" src/ -g "*.cs"` | Allocates full array upfront; `EnumerateFiles` streams results | `Directory.EnumerateFiles(...)` |
| M6 | `params` array allocation | `rg "params\s+\w+\[\]" src/ -g "*.cs"` | Every call allocates a new array; hot paths should use overloads | Add overloads for 1-3 args, or use `ReadOnlySpan<T>` |
| M7 | `Task.Delay` magic ms | `rg "Task\.Delay\(\d+\)" src/ -g "*.cs"` | Magic milliseconds obscure intent; E128050 requires TimeSpan for duration params | `Task.Delay(TimeSpan.FromMilliseconds(N))` |

### LOW Severity -- Style / Opportunity

| # | Pattern | Detection (`rg`) | Why It Matters | Fix |
|---|---------|-------------------|----------------|-----|
| L1 | `string.Format` over interpolation | `rg "string\.Format\(" src/ -g "*.cs"` | Interpolated strings are more readable and can use `string.Create` for perf | `$"...{value}..."` or `string.Create(CultureInfo.InvariantCulture, ...)` |
| L2 | `Encoding.UTF8.GetBytes` | `rg "Encoding\.UTF8\.GetBytes\(" src/ -g "*.cs"` | Span overload available on .NET 8+; avoids allocation | `Encoding.UTF8.GetBytes(chars, destination)` |
| L3 | Unsealed non-abstract class | `rg "public class\b" src/ -g "*.cs"` | JIT devirtualization works better with sealed classes; hot-path types benefit most | Add `sealed` to classes not designed for inheritance |
| L4 | `static readonly Dictionary` | `rg "static readonly Dictionary<" src/ -g "*.cs"` | Reinforces E128027; FrozenDictionary has faster lookup | `.ToFrozenDictionary()` |
| L5 | `Encoding.UTF8.GetString` alloc | `rg "Encoding\.UTF8\.GetString\(" src/ -g "*.cs"` | Creates new string; consider Span-based alternatives when downstream accepts spans | Check if consumer accepts `ReadOnlySpan<char>` |

## Triage Notes

- **Cold paths.** Don't flag patterns in startup code, CLI argument parsing, or error handlers. Focus on request-processing and batch-processing paths.
- **M1 capacity hint.** Only a finding when the surrounding method knows the size (loop bound, `.Count`, parameter). Grep alone can't determine this — manual review required.
- **M4 IGrouping.Count() exclusion.** LINQ `GroupBy` materializes groups eagerly into `Grouping<K,V>` which implements `ICollection<T>`. `.Count()` on `IGrouping` resolves to O(1) via fast-path — never a performance anti-pattern. Skip all `.Count()` on GroupBy results.
- **E128 dedup.** If a finding overlaps with an E128 rule (see exclusion list), skip it silently.
- **Agent modes.** The performance-focused review agent supports `--high` (HIGH-only, ~10 turns), `--medium` (MEDIUM-only, ~20 turns), and full scan (~30 turns). For 300+ file scopes, prefer a split mode over full scan.