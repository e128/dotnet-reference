# Unsafe And Performance Analyzer Tasks

*Updated: 2026-10-27T18:20:53Z*

Plan: [plan](unsafe-perf-analyzers-plan.md). Context: [context](unsafe-perf-analyzers-context.md).

## Phase 0: Pre-Flight

- [x] Run `scripts/status.sh` and confirm a clean working tree.
- [x] Confirm `E128112` is free.
- [x] Create `src/E128.Analyzers/Unsafe/`.
- [x] Confirm no `AllowUnsafeBlocks` property exists in the repo.
- [x] Read `lode/dotnet/analyzers/new-analyzer-checklist.md` and `code-fix-patterns.md`.

## Phase 1: Build

One rule at a time. Every rule got an analyzer test file and, where a fix
exists, a code fix test file.

- [x] E128112 `Unsafe.As` to `Unsafe.BitCast`.
- [x] E128113 `Unsafe.Read` and `Unsafe.Write` to the unaligned variants.
- [x] E128114 `Unsafe.Add` over `MemoryMarshal.GetArrayDataReference` to an indexer.
- [x] E128115 `Unsafe.As<int,bool>` and `Unsafe.As<bool,int>` to a comparison.
- [x] E128116 `MethodImplOptions.AggressiveOptimization`.
- [x] E128117 `[DllImport]` to `[LibraryImport]`.
- [x] E128118 `MemoryMarshal.Read<T>` and `Cast` on a non-blittable type.
- [x] E128119 fixed-size buffer field to `[InlineArray]`.
- [x] E128120 `Unsafe.SkipInit` on a local.
- [x] E128121 element-wise equality loop to `SequenceEqual`.
- [x] E128122 logger argument built before the level check.
- [x] E128123 runtime `Regex` field to `[GeneratedRegex]`.
- [x] E128124 `SearchValues.Create` outside a static readonly field.
- [x] E128125 `Unsafe.AsPointer` outside a `fixed` statement.
- [x] E128126 write through `fixed (char*)` on a string.
- [x] E128127 `Unsafe.NullRef<T>()` and `Unsafe.AsRef<T>((void*)0)`.
- [x] E128128 `Marshal.GetFunctionPointerForDelegate` with no lifetime anchor.
- [x] E128129 `Unsafe.Copy` over a type that contains a reference.
- [x] Mark each diagnostic-only rule as "no code fix" in the README row.

## Phase 2: Documentation And API Surface

- [x] Add the rule rows to `AnalyzerReleases.Unshipped.md`.
- [x] Add the README rows and the per-rule sections.
- [x] Add two `PublicAPI.Unshipped.txt` lines per new type.
- [x] Run `scripts/check.sh`.

## Phase 3: Release

- [ ] Run `scripts/internal/analyzer-release-check.sh`.
- [ ] Move the shipped rows from `Unshipped` to `Shipped`.
- [ ] Run `scripts/internal/version-bump.sh E128.Analyzers`.
- [ ] Run `/readme-check`.
- [ ] Ship through `/yeet`.

## Definition of Done

- [x] 18 rule IDs exist and appear in the README table.
- [x] 13 rules offer a code fix.
- [x] `scripts/check.sh` passes on a clean tree.
- [x] No new rule fires on the repo's own source.
- [ ] The release is published.
