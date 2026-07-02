# Lode Strategic Profile Template

Write the strategic profile to `lode/strategy/{YYYY-MM-DD}-{repo-slug}.md` for durable project memory.

```markdown
# Strategic Profile — {repo-slug}
*Recorded: {timestamp}*
*Source: martinizing audit — full report at {$report_path}*

## Choice Cascade (derived from code)

### Q1. Winning Aspiration
{what the code enables users to achieve — desired customer action}

### Q2. Where to Play
{arena, WTP dimensions, revealed non-goals, de-prioritized areas (angst test)}

### Q3. How to Win
{revealed differentiators, table stakes, operating imperatives}
{can't/won't test results for each differentiator}

### Q4. Capabilities
{reinforcing system, capability chain, dead capabilities, dependency leverage}
{keystone capability and its investment level}
{WWHTBT conditions for the primary WTP/HTW combination}

### Q5. Management Systems
{CI/test/build/metrics — support or contradict HTW?}
{portfolio governance mechanism — present or absent?}

### Cascade Coherence
{do adjacent levels reinforce? contradictions found?}
{plan vs strategy assessment — is this a bet or an initiative list?}

## Top Findings Summary
{top 3-5 HIGH severity findings from Phase 4 report, one line each}
```

If the audited path is not the repo root, derive `repo_slug` from the path argument.
