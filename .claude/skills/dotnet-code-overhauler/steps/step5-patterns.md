# Performance Review — Grep Patterns & Checklist

Reference file for Step 5 of dotnet-code-overhauler. Contains anti-pattern grep patterns
and analysis checklist previously embedded in the `dotnet-performance-analyst` agent.

## Anti-Pattern Grep Patterns

```
# Allocations in hot paths
\$"                              # string interpolation (allocates)
string\.Format\(                 # string.Format (allocates)
\+ "                             # string concatenation with +
\.ToString\(\)                   # boxing or unnecessary string conversion

# LINQ in hot paths (iterator + closure allocations)
\.Select\(|\.Where\(|\.OrderBy\(|\.GroupBy\(
\.ToArray\(\)                    # unnecessary materialization
\.Any\(\)|\.Count\(\)            # full enumeration when .Count property exists
\.First\(\)|\.Last\(\)           # may enumerate more than needed

# Boxing and allocation patterns
params\s+\w+\[\]                 # params array allocated per call
new\s+(List|Dictionary|HashSet)  # collection allocation (check if poolable)
Tuple\.Create\(                  # Tuple allocates (use ValueTuple)
Task\.FromResult\(               # consider ValueTask for hot paths

# I/O and async issues
\.Result[^s]|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult\(\)  # sync-over-async
Task\.Run\(                      # thread pool hop (unnecessary in async context)

# Reflection and dynamic dispatch
typeof\(.*\)\.GetMethod|\.GetProperty|\.GetField
Activator\.CreateInstance
\.InvokeMember\(|\.Invoke\(
dynamic\s+\w+                    # dynamic keyword (DLR overhead)

# Exception-driven control flow
catch\s*\(.*\)\s*\{[^}]*return   # catch-and-return as branching
```

## Analysis Checklist

| # | Check | Severity if found |
|---|-------|------------------|
| 1 | Sync-over-async (.Result, .Wait()) in request paths | CRITICAL |
| 2 | Allocations inside loops processing >100 items | HIGH |
| 3 | N+1 queries / unbatched I/O (per-item HTTP/DB calls in a loop) | HIGH |
| 4 | Large Object Heap pressure (buffers >85KB without ArrayPool) | HIGH |
| 5 | String concatenation in loops (use StringBuilder) | HIGH |
| 6 | LINQ chains in hot paths (consider manual loops) | MEDIUM |
| 7 | Unnecessary ToList()/ToArray() before iteration | MEDIUM |
| 8 | Reflection in hot paths (cache delegates or use source generators) | MEDIUM |
| 9 | Exception-driven control flow (TryParse instead of Parse+catch) | MEDIUM |
| 10 | Missing ConfigureAwait(false) in library code | LOW |
| 11 | Task.FromResult where ValueTask would avoid allocation | LOW |
| 12 | params arrays in frequently called methods | LOW |

## Severity Definitions

- **CRITICAL**: Causes measurable latency or throughput degradation under normal load. Fix before release.
- **HIGH**: Significant allocation pressure or wasted CPU. Visible in profiling under moderate load.
- **MEDIUM**: Inefficiency that matters at scale. May not show in benchmarks for small inputs but degrades with growth.
- **LOW**: Minor optimization opportunity. Only worth fixing if the code is a proven hot path.

## Domain Knowledge

Check `${CLAUDE_SKILL_DIR}/lessons/*.md` for project-specific false positives and known-good patterns
that should not be flagged.

### Common anti-patterns to flag

- `File.ReadAllBytesAsync` on files that may exceed 10 MB — LOH pressure
- `Path.GetFullPath(directoryInfo.FullName)` — redundant; `DirectoryInfo.FullName` is already normalized
- `FileInfo(resolvedPath)` constructed just to call `.Length` when already expensive — prefer caching
