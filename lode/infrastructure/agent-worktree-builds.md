# Agent Worktree Builds
*Updated: 2026-11-05T04:06:10Z*

A subagent spawned with worktree isolation lands under
`.claude/worktrees/agent-<id>/`. That worktree holds a full copy of the
repository and a branch of its own.

## Verified Behavior

These runs build clean at the same commit:

- `scripts/build.sh --project E128.Analyzers` in a nested worktree.
- `scripts/build.sh --project E128.Analyzers.Tests` in a nested worktree.
- The same two builds, run side by side in two worktrees at once.

`ArtifactsPath` resolves through `$(MSBuildThisFileDirectory)`, so each worktree
writes its own `artifacts/` directory. Concurrent builds do not share output.

## The Unreproduced Failure

One batch of five delegated agents each reported about 500 errors in files they
did not touch: CS1591, S3267, and S3358. A direct probe did not reproduce it, so
the cause stays unknown. Do not record a cause for it without a reproduction.

## Dispatch Rule

Treat a phantom error report from a delegated agent as suspect. Discard its
build log and build in the primary checkout instead. The parent holds the
merged tree, and one build there costs less than five worktree builds.

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
