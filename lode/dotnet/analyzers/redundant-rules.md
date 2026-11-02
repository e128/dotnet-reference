# Redundant E128 Rules
*Updated: 2026-11-01T20:55:18Z*

An E128 rule is redundant when an installed third-party rule fires on the same code. The redundant rule gets `severity = none` in `.globalconfig` (section "E128"), even when the rule ships a code fix. The rule source stays in the project, so package consumers still receive the rule and its fix.

| Disabled rule | Covered by                       |
| ------------- | -------------------------------- |
| E128008       | VSTHRD002, SS034, MA0042         |
| E128020       | RCS1242                          |
| E128067       | S1643, SS058                     |
| E128075       | CA5394, S2245                    |
| E128088       | IDE0010, SS018, SS019            |
| E128096       | VSTHRD002, SS034                 |

E128007 (async void) overlaps S3168 and VSTHRD100 but stays enabled. Probe method: build a scratch project with one snippet per rule, then compare which diagnostic IDs fire on the same line. `scripts/build.sh --project` resolves projects under `src/` or `tests/`.

Related: [analyzers.md](../analyzers.md), [release-tracking.md](../release-tracking.md).
