# Overlapping E128 Rules
*Updated: 2026-11-09T23:44:15Z*

An E128 rule overlaps when an installed third-party rule fires on the same code. Every overlapping rule is enabled at `error` in `.globalconfig`, so one construct can report two diagnostics. A fix for either diagnostic clears both.

This file keeps its old name so existing links resolve. Until 2026-11-09 the eight rules below were set to `none` as redundant.

| Overlapping rule | Also reported by         |
| ---------------- | ------------------------ |
| E128008          | VSTHRD002, SS034, MA0042 |
| E128020          | RCS1242                  |
| E128067          | S1643, SS058             |
| E128075          | CA5394, S2245            |
| E128088          | IDE0010, SS018, SS019    |
| E128096          | VSTHRD002, SS034         |
| E128162          | IDISP016                 |
| E128193          | IDISP003                 |

E128162 and E128193 were probed against IDisposableAnalyzers. On the probe
snippet, IDISP016 fired on the `Dispose()` line and E128162 fired on the read
that follows. IDISP003 fired on the same assignment line as E128193.

E128007 (async void) overlaps S3168 and VSTHRD100. Probe method: build a scratch project with one snippet per rule, then compare which diagnostic IDs fire on the same line. `scripts/build.sh --project` resolves projects under `src/` or `tests/`.

Related: [analyzers.md](../analyzers.md), [release-tracking.md](../release-tracking.md).
