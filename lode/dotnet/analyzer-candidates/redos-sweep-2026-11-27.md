# ReDoS Sweep
*Updated: 2026-11-27T16:16:52Z*

This sweep reads the ReDoS prevention checklist supplied on 2026-11-27, plus
the three security shapes that the earlier checklist left open. It maps every
vulnerable pattern shape to the rule that owns it, and it records each shape
that no rule owns.

Evidence comes from the installed rule set (Meziantou 3.0.342, Sonar 10.37, the
SDK CA rules), the E128 sources in `src/E128.Analyzers`, and the pugworks tree.

## Verdict

| Checklist shape | C# form | Owner | Verdict |
| --------------- | ------- | ----- | ------- |
| Nested quantifier `^([a-z0-9]+)+$` | `[GeneratedRegex]` literal | E128014 | Owned |
| Nested quantifier in a runtime literal | `new Regex("(a+)+")` | E128123 at `error` forces `[GeneratedRegex]`, then E128014 | Owned by the funnel |
| Adjacent wildcards `^\s*(.*)\s*$` | `[GeneratedRegex]` literal | E128013 | Owned |
| Overlapping alternation `^(a\|ab)+$` | a quantified group whose branches share a first character | E128264 | Shipped |
| Optional separator `(\w+[-.]?)+@` | a repeated group whose last atom is optional | E128014 | Shipped |
| Three or more `.*` segments | a literal pattern with repeated wildcards | E128013 | Shipped |
| Atomic group `(?>...)` | a literal pattern | E128013 and E128014 skip it on purpose | Owned |
| Match timeout | `new Regex` with no `TimeSpan` | MA0009 | Owned |
| Match timeout on `[GeneratedRegex]` | an attribute with no `matchTimeoutMilliseconds` | E128011 | Owned |
| Timeout on a static call with a constant pattern | `Regex.IsMatch(value, "literal")` | MA0110 and E128123 force `[GeneratedRegex]` | Owned by the funnel |
| Timeout on a static call with a variable pattern | `Regex.IsMatch(value, pattern)` | E128265 | Shipped |
| Linear-time engine | `RegexOptions.NonBacktracking` | none | Declined, the input trust is not decidable |
| Input length bound | a regex over a whole document | E128227 bounds an HTTP body only | Declined, too noisy |
| Dependency audit | a regex inside a NuGet package | NuGet Audit | Not analyzable |

The funnel row matters. A constant pattern in a runtime `Regex` call is a build
error under E128123 and MA0110, so the author converts it to `[GeneratedRegex]`,
where E128013 and E128014 run. A constant pattern therefore reaches the pattern
rules without a second analyzer. Only a non-constant pattern escapes.

## New Rule: Overlapping Alternation In A Quantified Group

**Score**: 3/5. Frequency 0, coverage gap 1, expressibility 1, value 1.

`^(a|ab)+$` lets both branches consume the same `a`, so a failing suffix makes
the engine try every split. `^(a|a?)+$` and `^(?:foo|foobar)*$` hold the same
shape. The checklist names the rewrite: sort the branches longest first, or make
them mutually exclusive.

Detection reads the pattern literal of a `[GeneratedRegex]` attribute:

1. Find every group that carries an unbounded quantifier (`+`, `*`, or an
   unbounded `{n,}`).
2. Skip an atomic group, which already prevents the split.
3. Read the top-level alternation of the group body.
4. Compare the first atom of each branch pair. Report when two branches can
   match the same first character.

`GeneratedRegexHelpers` already supplies `HasTopLevelAlternation`,
`TryReadFirstAtomSpan`, and `AtomsCanMatchSameText`, so the rule is a short
extension of the shipped pattern parser.

The code fix sorts the branches by atom length, longest first. That order makes
the greedy branch take the longest match first and removes the split for the
common `(a|ab)` shape. A branch pair that stays ambiguous after the sort gets
the diagnostic only, because the safe rewrite depends on the language the
pattern accepts.

The pugworks tree holds no site. The score stays at 3 because frequency is 0.

## Detection Gap: Optional Last Atom In A Repeated Group

**Score**: 3/5. Frequency 0, coverage gap 1, expressibility 1, value 1.

`^(\w+[-.]?)+@$` lets the iteration boundary fall anywhere inside a run of word
characters, because the trailing `[-.]?` may match nothing. The engine then
tries 2^n splits on `"aaaa...!"`.

E128014 does not report it. `FirstAtomCanAlsoBeLast` compares the first and last
atom of the group body and stays silent when they cannot match the same text. In
`(\w+[-.]?)` the last atom is `[-.]`, which never matches a word character, so
the overlap test fails and the split goes unreported.

The fix belongs in E128014, not in a new rule. A second condition covers it: the
group carries an unbounded quantifier, the body holds an unbounded quantifier,
and the last atom is optional (`?`, `*`, or a bounded `{0,n}`). The boundary can
then move between iterations, which is the same defect the rule exists to catch.

A group whose last atom is optional but whose body holds no quantifier stays
silent, as in `(ab?)+`, because the split count stays linear.

## New Rule: Three Or More Unbounded Wildcards

**Score**: 3/5. Frequency 0, coverage gap 1, expressibility 1, value 1.

`.*error.*timeout.*` costs O(n^3) on a line that almost matches. Each `.*`
scans the rest of the line, and a failing suffix repeats the scan from every
start. E128013 reads one adjacency only: a `\s*` beside a `.*` or a `.+`.

Detection counts unbounded `.` atoms in the pattern literal, outside a character
class and outside an atomic group. Three or more reports. Two reports only when
the pair is adjacent, which E128013 already owns.

The safe rewrite replaces each `.*` with a negated class, as in
`^[^\n]*error[^\n]*timeout`. The rule stays diagnostic only. Under `Singleline`
the rewrite needs the intent of the caller, so no code fix ships.

The rule keys on three wildcards rather than two because two is the common,
sound shape for a prefix and a suffix scan. Three is the point where the cost
becomes polynomial in a way that a crafted line can exploit.

## New Rule: Non-Constant Regex Pattern Without A Bound

**Score**: 3/5. Frequency 0, coverage gap 1, expressibility 1, value 1.

`Regex.IsMatch(value, pattern)` and `new Regex(pattern)` accept any pattern the
caller supplies. A caller can then inject a nested quantifier or an overlapping
alternation, and no build-time rule inspects it. This is the regex injection
sink that CA3012 used to cover. CA3011 and CA3012 no longer ship.

MA0009 does not close it. The rule reads the `new Regex` constructor and the
`[GeneratedRegex]` attribute. It does not report a static call, and it cannot
read a pattern it cannot see.

Detection reports a `Regex` constructor or a static `Regex.Match`, `IsMatch`,
`Replace`, or `Split` call when all three hold:

1. The pattern argument is not a compile-time constant.
2. No `TimeSpan` argument and no `matchTimeout` setting applies to the call.
3. The `RegexOptions` argument does not carry `NonBacktracking`.

The pugworks site stays silent. `ContentFilter.BuildCustomPatterns` passes a
one-second timeout, so the rule respects the mitigation the author chose.

No code fix applies. The intent of the caller decides between `Regex.Escape`, a
validation of the pattern, and a timeout.

## Open Security Shapes From The Earlier Checklist

The 2026-11-27 vulnerability sweep closed its own table. Three shapes in that
table remain open and enter this sweep as rows.

- **A certificate chain whose `Build` result is ignored, or whose policy drops a
  check.** `chain.Build(certificate)` returns a `bool`, so a bare call statement
  discards the answer. `X509ChainPolicy.VerificationFlags` with
  `AllowUnknownCertificateAuthority`, `IgnoreNotTimeValid`, or
  `IgnoreWrongUsage`, and `RevocationMode.NoCheck`, weaken the chain without
  touching the callback that E128156 owns.
- **A decompression loop with no cumulative output bound.** A read loop over
  `GZipStream`, `DeflateStream`, `BrotliStream`, or a `ZipArchive` entry that
  writes into a `MemoryStream` with no total cap decompresses a bomb. E128227
  bounds an HTTP body only.
- **A symmetric key set from a text encoding instead of a key derivation
  function.** `aes.Key = Encoding.UTF8.GetBytes(password)` takes a low-entropy
  secret with no salt and no work factor. The FIPS family covers key length and
  algorithm choice, never key derivation.

No row held a site in pugworks on 2026-11-27. The batch shipped each as a
guard rail: E128266 for the certificate chain, E128267 for the decompression
bound, and E128268 for the symmetric key source.

## Declined

- **Require `RegexOptions.NonBacktracking` on every pattern.** Whether the input
  is untrusted is not decidable at build time, and `NonBacktracking` rejects
  backreferences and lookaround, which some patterns need. The option belongs in
  the rule that can prove a bad shape, not in a blanket rule.
- **Bound the input length before every regex call.** The right cap comes from
  the feature, not from the call. E128227 already bounds the HTTP body, which is
  the one untrusted boundary the tree reads.
- **A rule that flags a `catch (RegexMatchTimeoutException)`.** The handler that
  swallows the timeout is the fail-open shape, and the guard-predicate candidate
  in [index.md](index.md) already owns it. A second rule would double-report.
- **A dependency audit for a regex inside a package.** NuGet Audit reads the
  package graph. A Roslyn rule cannot see into a compiled dependency.
- **A hand-rolled parser.** The checklist names it, but a parser is a design
  choice. No local shape separates a parser from any other loop.

## Related

- [index.md](index.md): the open register
- [skipped-decisions.md](skipped-decisions.md): every declined shape
- [vulnerability-sweep-2026-11-27.md](vulnerability-sweep-2026-11-27.md): the
  earlier security read on the same day
- [catalog.md](catalog.md): the shipped rules
