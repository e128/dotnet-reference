---
name: namespace-migration
description: >
  Migrate C# types between namespaces across the solution. Updates usings, refs, and verifies build.
when_to_use: >
  migrate namespace, rename namespace, namespace refactor, move types to namespace.
argument-hint: "<OldNamespace> <NewNamespace>"
---

# Namespace Migration

You migrate C# types to new namespaces across the entire solution, updating usings and refs and verifying the build.

## Step 1: Build the mapping table

From `$ARGUMENTS` or the conversation, extract the namespace mapping.
Expected format: `OldNamespace → NewNamespace` (one or more pairs).

If the mapping is unclear, ask once:
```
What's the namespace mapping? Example:
  Foo.Core → Foo.Pipeline
  Foo.Core.Models → Foo.Pipeline.Models
```

## Step 2: Audit impact

For each old namespace, find affected files:

```bash
rg -l "namespace {OldNamespace}" src/ -g "*.cs" | sort
rg -l "using {OldNamespace}" src/ tests/ -g "*.cs" | wc -l
```

Also check for `InternalsVisibleTo` implications:
```bash
rg -l "InternalsVisibleTo" src/ -g "*.csproj" -g "*.cs"
```

Print the impact summary inline and proceed immediately:
```
## Migration Impact: {OldNamespace} → {NewNamespace}

- Source files with namespace declaration: {N}
- Files with using directives: {M}
- InternalsVisibleTo affected: {Y/N — list if yes}
- Estimated build risk: LOW/MEDIUM/HIGH
```

## Step 3: Apply migration

Produce a clean build with all namespace references updated and git history preserved.

- Use `git mv` for any file moves (plain `mv` loses history; if `git mv` errors on untracked files, `git add` first)
- Use Edit tool for namespace declarations and using directives (not sed/awk — can corrupt encoding)
- Update every occurrence: `namespace` declarations, `using` directives, fully-qualified references `{OldNamespace}.TypeName`, `<RootNamespace>`/`<AssemblyName>` in `.csproj`/`.props`, and any `InternalsVisibleTo` strings

Convention: `Namespace.Sub.Name` maps to `src/{Project}/Sub/Name/`.

Verify with:
```bash
scripts/build.sh
```

If build errors remain, they are almost always residual using directives or fully-qualified type references. Read the error, find the file, apply the fix. Re-run until clean.

Output the test command — do not run it:
```
scripts/test.sh --all
```

Report completion:
```
## Migration Complete: {OldNamespace} → {NewNamespace}

- Files moved: {N}
- using directives updated: {M}
- Build status: CLEAN
- Test command: {command}
```

## Notes

- **Never use `mv` — always `git mv`** to preserve history
- If `git mv` errors on untracked files, `git add` the file first
- After a large migration, `scripts/format.sh --changed` may reorder using directives — that's expected
- InternalsVisibleTo uses assembly names, not namespace names — check both
- If the old namespace is a prefix of another (e.g. `Foo` and `Foo.Core`), be precise
  with grep patterns: use `\bFoo\b` or `namespace Foo;` to avoid partial matches
