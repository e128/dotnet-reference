# Token Validation Flag Pinned False: Plan
*Updated: 2026-11-14T00:08:09Z*

Ship E128228: a `TokenValidationParameters` flag set to a literal `false`
silently disables a JWT check. Register row: "Token validation flag set to
constant `false`" in [lode/dotnet/analyzer-candidates/index.md](../../lode/dotnet/analyzer-candidates/index.md).

## Success Criteria

1. The rule has an analyzer, tests, a README row, and a README section.
2. `scripts/check.sh` passes with zero warnings.
3. The rule appears in `AnalyzerReleases.Unshipped.md`.
4. The lode register records the shipped rule, and the Tier 2 row leaves the table.
5. The evidence question in Phase 0 has a recorded answer.

## Phases

### Phase 0: Evidence

1. Search every repository under `~/repos/e128` for `TokenValidationParameters`.
2. Record the site count as of the plan date.
3. If the count stays zero, decide with the maintainer: ship for prevention, or
   close the row as skipped. Record the decision.

### Phase 1: Tests

1. Initializer sets each of the six flags to `false` and the test expects a hit.
   The six flags: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`,
   `ValidateIssuerSigningKey`, `RequireExpirationTime`, `RequireSignedTokens`.
2. Assignment after construction expects a hit.
3. A literal `true`, a non-constant value, and a call argument stay silent.
4. An unrelated type with a same-named property stays silent.

### Phase 2: Analyzer

Diagnostic only. The safe value depends on the deploy, so no code fix.

1. Match the property symbol against the `TokenValidationParameters` type.
2. Report an object initializer assignment or a simple assignment whose value is
   `false`.
3. Report only a literal `false`, so a computed value stays silent.

### Phase 3: Documentation

1. Add the README row and section.
2. Add the `AnalyzerReleases.Unshipped.md` row.

### Phase 4: Register And Verification

1. Move the Tier 2 row to the catalog with the rule ID.
2. Add the detection shape to `rule-details-security.md`.
3. Run `scripts/check.sh` and `scripts/lode-check.sh --changed`.

## Out Of Scope

- Missing flags, such as a parameters object with no `ValidateLifetime` set.
- A flag read from configuration. The value is not a literal, so the rule cannot
  judge it.
