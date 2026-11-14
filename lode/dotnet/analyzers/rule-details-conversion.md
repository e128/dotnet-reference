# Conversion Rules E128223 to E128226
*Updated: 2026-11-14T01:26:59Z*

Detection shape for the .NET 11 conversion family. The rules move working code onto a
mechanism the band carries. Sources: [net11-conversion-sweep.md](../analyzer-candidates/net11-conversion-sweep.md).
The rule text and the examples live in `src/E128.Analyzers/README.md`.

## Shared Shape

Each rule reads one method body. A rule reports only the shape it can prove, and it keeps
silent when the rewrite could change behavior. E128223 and E128224 carry no code fix,
because the replacement moves error handling or assumes a read-only consumer. E128225
carries a code fix, because the modifier change is local. E128226 carries no code fix,
because the rewrite moves a loop into an initializer.

Scopes come from `BlockScope.GetScope`. A lambda body is the scope for a node inside a
callback. A field initializer has no scope, so a field declaration stays silent.

## E128223: Hand-Rolled Child-Process Capture

Reports `WaitForExit` or `WaitForExitAsync` on a process when all of these hold:

1. A `ProcessStartInfo` flag for `RedirectStandardOutput` or `RedirectStandardError` is
   `true` on the start info that belongs to the process.
2. `RedirectStandardInput` is not `true`.
3. A `ReadToEnd` or `ReadToEndAsync` call on `StandardOutput` or `StandardError` of the
   same process starts before the wait.

The start info binds through the `StartInfo` member, a start info local passed to
`Process.Start`, a start info assigned to the process, a process object initializer,
or a statement-form flag set on a start-info local that later reaches a process
(`FlagBinds` routes such a receiver through `LocalLinksToProcess`).

## E128224: Read-Only MemoryStream

Reports `new MemoryStream(x)` in a local when `x` is an `Encoding.GetBytes` result or a
`byte[]`, and every use of the local is read shaped. Read shaped means one of:

- A member call in the read set (`Read`, `ReadAsync`, `CopyTo`, `Seek`, `Dispose`, and
  similar).
- A property read from the read set (`Length`, `Position`, `CanRead`, and similar).
- An argument to a read consumer (`StreamReader`, `BinaryReader`, `GZipStream`,
  `XPathDocument`, `XmlReader.Create`, `JsonDocument.Parse`, `XDocument.Load`,
  `HashAlgorithm.ComputeHash`).

A compression wrapper (`GZipStream`, `DeflateStream`, `BrotliStream`) counts as a
read consumer only when the creation passes `CompressionMode.Decompress`
(`IsDecompressMode` resolves the field symbol). A compress-mode or mode-less
wrapper writes to the inner stream, so it keeps the report silent.

A write call, `ToArray`, an unknown consumer, a return, or a store into a member keeps
the report silent. The target types reject a write, so a wrong report would break code.

## E128225: Abstract Base With Members

Shares the analysis in `ClosedHierarchy` with E128219. The candidate is an internal
abstract class that declares members, is not already closed, and has at least one
derivation. The report fires only when every derivation lives in the same assembly.

E128219 covers the memberless base. A record declaration stays silent. The code fix
replaces `abstract` with `closed` through `MemberlessAbstractBaseCodeFixProvider`.

A `closed` class may declare `abstract` members. The SDK 11.0.100 probe confirms it. The
compiler rejects only the `closed abstract` modifier pair on the type.

## E128226: Comparer Set Built By A Loop

Reports `new HashSet<T>(comparer)` in a local when:

1. The creation has no collection initializer.
2. The argument implements `IEqualityComparer<T>`.
3. Every `Add` call on the local sits inside a loop.
4. Every other use of the local is `Contains`, `TryGetValue`, or `Count`.

A field initializer, an escaping set, and an `Add` outside the loop stay silent.

## Verification

Each rule has a test class in `tests/E128.Analyzers.Tests/`. The tests use the net10
reference set, because every rule binds a type that predates the band. The .NET 11 target
type appears only in the message and in the fix.

## Related

- [rule-details-union.md](rule-details-union.md): E128219 to E128221, the union family
- [authoring.md](authoring.md): analyzer registration and test patterns
- [../net11-upgrade.md](../net11-upgrade.md): the band rules
