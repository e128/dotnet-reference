# Build Tiebreaker

The build is the authoritative source for warnings and errors. `mr-sparkle-dotnet` reports it through `scripts/diagnostics.sh --group`.

When the build reports zero warnings and zero errors, mark an unconfirmed Medium or Low compliance finding `(advisory, build clean)`.

Diff-based reviewers can miscalculate line numbers from diff context. Cross-reference every compliance finding with the build result before you escalate it. This matters most when a reviewer cites a line number that does not exist in the file.

House rules that analyzers do not enforce belong in `.claude/rules/`, not in this skill. `SKILL.md` owns the report shape.
