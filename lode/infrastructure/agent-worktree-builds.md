# Agent Worktree Builds
*Updated: 2026-11-16T20:16:49Z*

A subagent spawned with worktree isolation lands under
`.claude/worktrees/agent-<id>/`. That worktree holds a full copy of the
repository and a branch of its own.

## Verified Behavior

`ArtifactsPath` resolves through `$(MSBuildThisFileDirectory)`, so each worktree
writes its own `artifacts/` directory. Concurrent builds do not share output.

A build that finds current output in `artifacts/` skips compilation and
reports success. That result does not show that the worktree compiles.

## The Nested Globalconfig Collision

A forced rebuild in a nested worktree fails with phantom errors in files nobody
touched. A rebuild of `E128.Analyzers` with `-t:Rebuild` reports RS0016, CS1591,
RS0017, S3267, IDE0046, and S3358.

The SDK targets (`Microsoft.Managed.Core.targets`) add the `.globalconfig` of
every ancestor directory to `GlobalAnalyzerConfigFiles`. A nested worktree
therefore loads two files: its own and the primary checkout root file. Both set
`global_level = 100`. Roslyn drops the keys that two global configs set at the
same level. The rules that the file sets to `none` then fall back to the
blanket error severity.

Check the evaluation with
`dotnet msbuild <project> -getItem:EditorConfigFiles` and look for both root
`.globalconfig` paths.

## Dispatch Rule

Do not build or test inside a nested worktree. Treat an error report from a
worktree agent as this collision until the evaluation shows one root file.

Run delegated test writers in the primary checkout, one agent at a time,
because builds share `artifacts/`. A worktree outside the repository tree has
no ancestor root `.globalconfig`, so the collision cannot occur there. Do not
edit `global_level` or add a targets file to bypass the collision.

Give each agent write access to its own new files only. The parent edits the
shared surfaces: `src/E128.Analyzers/README.md`,
`src/E128.Analyzers/PublicAPI.Unshipped.txt`,
`src/E128.Analyzers/AnalyzerReleases.Unshipped.md`, and `lode/`.

## Stale Worktrees

Remove a worktree with `git worktree remove --force <path>`, then run
`git worktree prune`. A registration can outlive its directory. Prune clears the
dangling entry.

## Related

- [claude-code-maintenance.md](claude-code-maintenance.md): harness tier map
- [../dotnet/analyzers/rule-details.md](../dotnet/analyzers/rule-details.md):
  where a new rule records its detection shape
