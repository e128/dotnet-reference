# Code Quality Review Context

*Updated: 2026-10-31T21:01:10Z*

## Target

| Field       | Value                                                        |
| ----------- | ------------------------------------------------------------ |
| Base        | `a1a70a5` on `main`                                          |
| Head        | uncommitted working tree                                     |
| Files       | 26. 14 tracked, 12 new                                       |
| Tracked diff | +458, -188                                                  |
| New files   | 12 files, 1416 lines                                         |
| Commits     | 0 in the target                                              |
| Plan        | none. The deleted `guidance-analyzers-batch-2` covers E128131 to E128138 |

The eight commits merged today, `#110` to `#116`, are excluded. Each one passed
pull request review.

## Vectors Run

| Reviewer                    | Scope                                     | Outcome            |
| --------------------------- | ----------------------------------------- | ------------------ |
| `security-reviewer`         | Vendored. All 3 slices and full sources   | 4 findings         |
| `code-reviewer`             | Vendored. All 3 slices and full sources   | 10 findings        |
| `adversarial-review`        | 3 attackers, then 1 defender              | 13 findings        |
| `code-review`               | Built-in skill, effort high               | 10 findings        |
| `ponytail:ponytail-review`  | Lean and over-engineering                 | 2 findings         |
| `mr-sparkle-dotnet`         | .NET lens, `--perf`, `--test-quality`     | gates clean        |

## Ground Truth

| Gate                                          | Result                         |
| --------------------------------------------- | ------------------------------ |
| `scripts/build.sh`                            | 0 warnings, 0 errors           |
| `scripts/format.sh --check`                   | clean                          |
| `scripts/test.sh --all`                       | 1377 passed, 3 skipped         |
| New test classes, all six                     | 19 passed, 0 failed            |
| `scripts/internal/analyzer-release-check.sh`  | clean                          |
| `scripts/readme-table-diff.sh --analyzer`     | clean                          |
| `scripts/suppression-scan.sh`                 | no new suppression             |
| `scripts/violation-scan.sh`                   | 3 hits, all in analyzer source |

The three violation-scan hits are the analyzer definitions that detect those
patterns. They are false positives. The build being clean makes an unconfirmed
Medium or Low compliance finding advisory.

`scripts/test.sh <ShortClassName>` reports zero tests and exit status `ok`. A
fully qualified name is required. Six test classes were reported as empty
before the qualified name was used.

## Dedup Report

| Merged finding                             | Reviewers                                                                    |
| ------------------------------------------ | ---------------------------------------------------------------------------- |
| E128141 emits a bare literal for `Span<byte>` | security-reviewer, code-reviewer, functionality attacker, quality attacker |
| E128140 maps a zero literal to `Clear()`   | security-reviewer, functionality attacker                                    |
| E128140 `IsSimpleTarget` accepts a property | functionality attacker, quality attacker                                    |
| E128141 hard cast of `SyntaxTree.Options`  | code-reviewer, quality attacker                                              |
| E128140 `KindKey` has no reader            | code-reviewer, quality attacker                                              |
| README unsafe table row width              | code-reviewer, quality attacker                                              |
| Lode third-read table misaligned           | code-reviewer, quality attacker                                              |
| `IsZeroValue` `DefaultExpressionSyntax` arm is unreachable | lead reviewer, quality attacker                          |

The `DefaultExpressionSyntax` arm never runs. `IsRepeatableValue` returns false
for that node before `IsZeroValue` is called.

## Defender Pass

| Finding                                                        | Verdict                                       |
| -------------------------------------------------------------- | --------------------------------------------- |
| H1, E128141 emits `"abc"u8` for a `Span<byte>` target           | CONFIRMED, High                               |
| H2, E128139 fix emits `(expr != 0)` for a `bool` typed argument | REJECTED                                      |

Only the lead reviewer raised H2. Two reviewers called the E128139 fix sound.

The defender rejected H2 on the API signature. `Unsafe.ReadUnaligned<T>` binds
only `void*`, `ref byte`, and `ref readonly byte`. A `ref bool` or `ref int`
argument fails an identity conversion, so no diagnostic exists on that code.
The fix guard also requires a `ref` keyword, which excludes the `void*`
overload. Every call the fix can reach has a `byte` expression, and `byte != 0`
is valid.

Defender note: the guard rests on call binding, not on an explicit type check.
A test for the `in` and `ref readonly` overload would close that gap.

## Reviewer Notes

`code-review` ran in the background and returned 10 findings. Two were new. I checked both by hand before the merge.

- `Unsafe/UnsafeUnalignedNonBlittableCodeFixProvider.cs:78` rewrites an
  expression statement into `(source != 0);`. C# allows only an invocation,
  assignment, increment, object creation, or await as a statement. The result is
  CS0201.
- `lode/dotnet/analyzer-candidates.md:106` says E128139 never held an ID.
  `HEAD` assigns E128139 to the `Debug.Assert` guard at line 89. The claim is
  false.

The remaining eight either duplicate a merged finding or report a table width
and a sentence length. No reviewer returned nothing. No reviewer failed.

`scripts/test.sh` with a short class name reports zero tests and exit status
`ok`. A qualified name is required. This is a script defect, not a diff defect.
