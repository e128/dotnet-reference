# Analyzer Candidates
*Updated: 2026-10-27T18:20:50Z*
*Source: analyzer-review-miner 2026-04-15, evidence window 2026-04-12..2026-04-15*

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
| E128070 | pool-rent-capacity-guard               | implemented | n/a   | 2026-05-09 |
| E128071 | fips-unapproved-hash                   | implemented | n/a   | 2026-05-10 |
| E128072 | sha256-create-obsolete                 | implemented | n/a   | 2026-05-10 |

## Rules From Guidance Documents

Two published guidance documents seeded the rules below. Most rules carry a code
fix provider. The rules E128125 to E128129 carry a diagnostic only.

- Source A: the .NET performance skill guide in `EgorBo/BlazingFastDotnet`.
- Source B: the runtime unsafe-code best-practices page in `dotnet/docs`.

This table records the rule IDs so a later session can check for ID reuse. The
rule description and the fix shape live in `src/E128.Analyzers/README.md`.

| ID range        | Domain      | Code fix        | Category directory |
| --------------- | ----------- | --------------- | ------------------ |
| E128112-E128115 | Unsafe code | every rule      | `Unsafe/`          |
| E128116-E128117 | Performance | every rule      | `Performance/`     |
| E128118-E128120 | Unsafe code | every rule      | `Unsafe/`          |
| E128121-E128124 | Performance | every rule      | `Performance/`     |
| E128125-E128129 | Unsafe code | no rule         | `Unsafe/`          |

The next free ID after this set is E128130.

These rules have no code fix. The remediation depends on intent that the syntax
tree does not carry.

- E128125: the pin scope is a caller decision.
- E128126: the replacement depends on how the new string is built.
- E128127: safe code has no null byref equivalent.
- E128128: the anchor field must be placed by the user.
- E128129: the replacement depends on whether the caller needs a deep or a shallow copy.

The guidance rows that stay a document and not a rule:

| Row                                    | Why it stays a document                  |
| -------------------------------------- | ---------------------------------------- |
| Ordinal string comparison              | CA1307, CA1309, and CA1310 cover it       |
| `ToLower()` equality idiom             | Meziantou covers it                       |
| Runtime knob tuning                    | Environment variables, not source         |
| False sharing and cache-line padding   | Not visible to the compiler               |
| Thread-local and striped counters      | Detection needs runtime behavior          |
| `WebApplication.CreateSlimBuilder`     | Project-level choice, not a code defect   |
| Source-generated JSON registration     | Whole-project configuration, no local fix |
| Object header and MethodTable access   | No supported replacement                  |
| Fuzzing and raw IL validation          | Process guidance, not a code shape        |
| `AggressiveInlining` overuse           | The size heuristic is too noisy           |
