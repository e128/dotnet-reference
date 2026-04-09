# Step 9: Verify CI Tests

**Read baseline from disk** (never from memory):
```bash
cat .claude/tmp/overhauler/baseline.md
```

## CI Tests

Read `.claude/tmp/overhauler/test-convention.md` for the detected filter command.
If `build-validator` agent is available, spawn it — otherwise run the filter command from the convention file.
Compare result to baseline: same or higher count, no new failures or skips.

## Format Verification

```bash
dotnet format <solution> --verify-no-changes 2>&1
```

If files need reformatting, fix them before proceeding. Format compliance is non-negotiable.

## IDE Diagnostics (Enhanced Layer)

If `mcp__ide__getDiagnostics` is available, run it on all `.cs` files modified during the overhaul. This catches analyzer warnings that the build might not surface (IDE-only rules). Triage findings using the severity table from `conventions.md` if present, otherwise use defaults:
- HIGH = build errors + null-ref
- MEDIUM = analyzer violations + format
- LOW = style + naming
