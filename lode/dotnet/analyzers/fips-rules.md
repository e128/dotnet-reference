# FIPS Cryptography Rules
*Updated: 2026-11-03T03:14:50Z*

The Security analyzer set enforces organization cryptography policy. All rules
report an error, so a policy break fails the build by default. E128071 and
E128075 are the only rules with a code fix. The rule text lives
in `src/E128.Analyzers/README.md`. This file records the policy values and the
invariants that the rule set shares.

## Rule Map

| Rule    | Policy subject                                  |
| ------- | ----------------------------------------------- |
| E128071 | Blocked hash, cipher and padding algorithms     |
| E128075 | `System.Random` in a security context           |
| E128142 | Managed and CNG concrete type creation          |
| E128143 | RSA key size and named curve                    |
| E128144 | Cipher mode                                     |
| E128145 | PBKDF2 work factor and PRF                      |
| E128146 | TLS protocol version                            |
| E128147 | Obsolete `RNGCryptoServiceProvider`             |
| E128148 | Ciphers excluded by name                        |
| E128149 | AES-GCM tag size                                |
| E128150 | SHA-3, SHAKE and KMAC policy block              |
| E128151 | HMAC key length                                 |

## Policy Values

- Every rule reports an error. A policy break fails the build by default, so a
  consumer must fix the code or lower the severity in its own configuration.
- RSA keys use at least 3072 bits. Approved curves are P-384 and P-521.
- PBKDF2 uses at least 600,000 iterations.
- AES-GCM uses the full 16-byte tag.
- An HMAC key is at least as long as the hash output.
- TLS is 1.2 or later.
- Organization policy blocks SHA-3, SHAKE and KMAC. NIST does not block them.
  The rule reports policy, not a NIST prohibition.
- DSA signature generation is blocked. DSA verification of an existing
  signature stays allowed.
- The E128071 fix maps each blocked hash type to its SHA-2 replacement, so the
  digest of existing data changes. The E128075 fix maps `Random.Shared.Next` to a
  single replacement. The other rules leave the replacement choice to the caller.
- Every rule analyzes generated code with
  `GeneratedCodeAnalysisFlags.Analyze | ReportDiagnostics`. Generated crypto
  code runs in production, so it meets the same policy.

## One Diagnostic per Type

E128071 reports a blocked type. E128148 reports a type whose name matches an
excluded cipher. `FipsTypeClassifier` gives each type one verdict, so the two
rules never report the same type. E128148 skips a type that E128071 blocks,
because the type or a base type is in the E128071 set. E128071 stops its
factory-name analysis when E128148 owns the type, so a call site also produces
one diagnostic.

E128148 matches a role word only when it ends the type name. `IdeaEngine`
reports. `IdeaRankingAlgorithm` and `IdeaManagedService` do not.

`FipsTypeClassifier` caches the verdict per compilation, so each rule reads the
shared verdict and holds no private copy.

Both rules inspect the static members `Create`, `HashData`, `TryHashData` and
`HashDataAsync`, which produce a value without `new`.

## Shared Helper

`FipsSymbolHelpers` in `src/E128.Analyzers/Security/` holds symbol matching,
constant resolution, hash-name reporting and buffer-length reading. Rules call it
instead of matching source text. `FipsTypeClassifier` holds the type verdict.

Invariants:

- Type matching uses symbols. A `using` alias or `using static` cannot hide a
  blocked type.
- E128071 reads the assignment target member name before it walks the
  assignment. A deconstruction assignment always enters the walk, because its
  targets appear only after the split.
- A hash name resolves through a static property, a constructor, a
  `FromOid` call, a conditional, a `??` or a switch expression. It also resolves
  through an unreassigned local or a `readonly` field that no constructor and no
  `init` setter writes.
- A name that appears more than once at one call site or one store reports once.
- A collection probe such as `Equals` or `Contains` on a framework type is
  skipped. A method that application code declares is not skipped.
- A buffer length resolves from an array creation, a collection expression, a
  `stackalloc`, `Encoding.GetBytes` and `Convert.FromBase64String` or
  `Convert.FromHexString` with a constant argument.
- A buffer length also resolves through a slice or a range: `AsSpan`, `AsMemory`,
  `Slice`, `RuntimeHelpers.GetSubArray`, an index expression and an array range.
  A non-constant bound makes the length unknown.
- A compile-time unknown value is not reported. The rule does not guess.
- No rule reads memory or a runtime value.

## Hash Name Stores

A `HashAlgorithmName` that a wrapper receives as a variable is not resolved. The
store check reports a mutable field initializer, a property initializer and an
assignment to a `HashAlgorithmName` target. A `readonly` field resolves at each
use, so its initializer is not reported twice.

```csharp
private HashAlgorithmName _name = HashAlgorithmName.SHA1;          // reported
private static readonly HashAlgorithmName Ok = HashAlgorithmName.SHA256; // not reported
```

## Adding a Rule

Follow [new-analyzer-checklist.md](new-analyzer-checklist.md). Assign the next
free id from [analyzer-candidates.md](../analyzer-candidates.md). Add a row to
`AnalyzerReleases.Unshipped.md` and the README table in the same change. Run
`scripts/readme-table-diff.sh --analyzer`.

## Related

- [analyzers.md](../analyzers.md): analyzer strategy and the category map
- [analyzer-candidates.md](../analyzer-candidates.md): the ID map and free IDs
- [../../terminology.md](../../terminology.md): the FIPS term
