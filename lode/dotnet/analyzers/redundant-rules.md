# Redundant E128 Rules
*Updated: 2026-10-09T15:41:53Z*

An E128 rule is redundant when an installed third-party rule fires on the same code. A redundant E128 rule with a code fix stays enabled, because the fix adds value. A redundant rule without a code fix gets `severity = none` in `.globalconfig` (section "E128"). The rule source stays in the project, so package consumers still receive it.

| Disabled rule | Covered by                       |
| ------------- | -------------------------------- |
| E128008       | VSTHRD002, SS034, MA0042         |
| E128020       | RCS1242                          |
| E128067       | S1643, SS058                     |
| E128075       | CA5394, S2245                    |
| E128088       | IDE0010, SS018, SS019            |
| E128096       | VSTHRD002, SS034                 |

E128007 (async void) overlaps S3168 and VSTHRD100 but ships a code fix, so it stays enabled. Probe method: build a scratch project with one snippet per rule, then compare which diagnostic IDs fire on the same line. `scripts/build.sh --project` resolves projects under `src/` or `tests/`.

Related: [analyzers.md](../analyzers.md), [release-tracking.md](../release-tracking.md).
