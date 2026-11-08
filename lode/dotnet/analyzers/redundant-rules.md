# Redundant E128 Rules
*Updated: 2026-11-08T19:40:21Z*

An E128 rule is redundant when an installed third-party rule fires on the same code. The redundant rule gets `severity = none` in `.globalconfig` (section "E128"), even when the rule ships a code fix. The rule source stays in the project, so package consumers still receive the rule and its fix.

| Disabled rule | Covered by               |
| ------------- | ------------------------ |
| E128008       | VSTHRD002, SS034, MA0042 |
| E128020       | RCS1242                  |
| E128067       | S1643, SS058             |
| E128075       | CA5394, S2245            |
| E128088       | IDE0010, SS018, SS019    |
| E128096       | VSTHRD002, SS034         |
| E128162       | IDISP016                 |
| E128193       | IDISP003                 |

E128162 and E128193 were probed against IDisposableAnalyzers. On the probe
snippet, IDISP016 fired on the `Dispose()` line and E128162 fired on the read
that follows. IDISP003 fired on the same assignment line as E128193.

E128075 ships an Error as the FIPS default, so the `none` here applies only to
this repository. See [fips-rules.md](fips-rules.md) for the FIPS rule set.

E128007 (async void) overlaps S3168 and VSTHRD100 but stays enabled. Probe method: build a scratch project with one snippet per rule, then compare which diagnostic IDs fire on the same line. `scripts/build.sh --project` resolves projects under `src/` or `tests/`.

Related: [analyzers.md](../analyzers.md), [release-tracking.md](../release-tracking.md).
