# Code Review Self-Improvement

This skill must get better with every use. After a run, do these steps.

1. **Capture a new review category** when a run finds a class of issue that no rubric covers. Add it to the matching file in `references/`.
2. **Refine a reviewer prompt** when a reviewer returns low-signal findings. Edit its file in `references/`.
3. **Update severity calibration** when the same issue is consistently over-rated or under-rated. Edit the merge rules in `SKILL.md`.
4. **Record a codebase exception** when a legitimate pattern is flagged. Add it to `../review-apply/references/known-exceptions.md`.
5. **Refine the Roslyn severity map** when the navigator findings are consistently mis-rated. Edit `../../mr-sparkle-dotnet/references/dotnet-lens.md`.
6. **Record a deduplication miss** when one issue reaches the report twice. Edit the merge rules in `SKILL.md`.

The goal: each run produces higher signal-to-noise findings than the last.

Never record a run in `lode/`. A run is not project knowledge. A durable rule that changes how the code gets written belongs in `.claude/rules/`.
