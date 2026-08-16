# Analyzer Candidates
*Updated: 2026-08-16T12:36:40Z*
*Source: analyzer-review-miner 2026-04-15 — evidence window: 2026-04-12..2026-04-15*

No open candidate remains from the 2026-04-15 mining pass. Each of the three
candidates below now ships as a real analyzer. See the catalog table for the
rule ID, and see `src/E128.Analyzers/README.md` for the current rule
description.

- `mutable-static-readonly-array` shipped as E128061.
- `test-reference-assemblies-tfm-mismatch` shipped as E128062.
- `readonly-struct-property-no-init` shipped as E128074.

## Catalog

| ID      | Name                                   | Status      | Score | Last seen  |
| ------- | -------------------------------------- | ----------- | ----- | ---------- |
| E128061 | mutable-static-readonly-array          | implemented | 5/5   | 2026-04-15 |
| E128062 | test-reference-assemblies-tfm-mismatch | implemented | 4/5   | 2026-04-15 |
| E128074 | readonly-struct-property-no-init       | implemented | 3/5   | 2026-04-15 |
| E128070 | pool-rent-capacity-guard               | implemented | —     | 2026-05-09 |
| E128071 | fips-unapproved-hash                   | implemented | —     | 2026-05-10 |
| E128072 | sha256-create-obsolete                 | implemented | —     | 2026-05-10 |
