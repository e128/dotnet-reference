# Cross-File Consistency Review

Include when the diff spans 3+ non-source files (agents, skills, scripts, lode, config), or
when the diff is the result of a rename, multi-session plan, or merge resolution.

```
CROSS-FILE CONSISTENCY RULES (include when diff touches 3+ agent/skill/script/config files,
or after renames, multi-session plans, or merge resolutions):

Duplicate sections (bad merge artifact):
  [HIGH] Identical section block appearing twice in the same file — textbook merge artifact
         where two sessions appended the same block and the merge kept both
  [MEDIUM] Same rule stated in two different sections within one file (e.g., "Style rules"
           and "Critical Rules") with slightly different wording — merge into the more
           detailed version and delete the other

Rename residue (stale references after rename):
  [HIGH] Agent/skill/script referencing a source directory, project name, or domain that
         was renamed — grep for the old name across .claude/, lode/, and scripts/
  [HIGH] Domain example list in an agent prompt containing a domain that no longer exists
         (e.g., cruisesearch after rename to travelsearch)
  [MEDIUM] Lode files using old class/project/domain names in prose — the code was renamed
           but the documentation was not swept

Broken cross-references:
  [HIGH] Skill instruction referencing a file that no phase creates (e.g., "cat report.md"
         when only context.md is generated) — will cause file-not-found at runtime
  [MEDIUM] Agent prompt referencing a script flag that doesn't exist or was renamed
  [MEDIUM] Routing table entry pointing to a script path that no longer exists on disk

Functionally-invisible bugs:
  [MEDIUM] Redundant encoding/transformation that produces correct results by accident
           (e.g., double-encoding a hash where both copies double-encode identically, so
           comparison still works) — correct behavior masking incorrect implementation
  [MEDIUM] Script using a pipeline step that's a no-op in the current context but would
           break if the upstream output format changed

Extension method dead-code false positives:
  [HIGH] Extension class flagged as dead code because the class name has zero references —
         extension methods are called as instance methods; grep each public method name
         individually, not the containing class name
  [MEDIUM] Dead-code deletion that only grepped src/ but not tests/ — architecture test
           fixtures hold typeof(X).Assembly anchors that reference production types
```

**Post-rename verification protocol:** After any rename that touches directories, projects, or
domain names, the reviewer should verify:
1. `rg -i "OldName" .claude/ lode/ scripts/` — catches agent/skill/config references
2. `rg -i "OldName" tests/ -g "*.cs"` — catches test fixture anchors
3. Domain routing tables (arch-scan, keyword-shortcuts) updated
4. Lode domain directories and lode-map entries updated

**Activation heuristic:** Include this rubric when: diff touches 3+ files under `.claude/`,
`lode/`, or `scripts/`; commit message contains "rename", "migrate", "move", or "consolidate";
diff includes deletions paired with additions of similarly-named files; PR is labeled as merge
resolution or multi-session work.
