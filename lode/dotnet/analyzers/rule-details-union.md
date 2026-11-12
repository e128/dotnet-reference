# Rule Detail Notes: Union and Hierarchy Rules
*Updated: 2026-11-12T16:46:34Z*

Detection shape for the union rule family E128219 to E128221. The complete
rule table lives in `src/E128.Analyzers/README.md`. The union language surface
and its compiler behavior live in [net11-upgrade.md](../net11-upgrade.md).

The rules read the typed `UnionDeclarationSyntax` node. That node sits behind
`RSEXPERIMENTAL006` at the pinned Roslyn band. Both component projects take the
suppression through `<NoWarn>` in their csproj files, and the maintainer
approved it for this family.

### Shared union detection

`UnionTypes.IsUnion` accepts a type that either declares itself through
`UnionDeclarationSyntax`, or carries the compiler-synthesized interface. The
interface check matches the name `IUnion` and the namespace chain
`System.Runtime.CompilerServices` by string. `GetTypeByMetadataName` returns
null for the synthesized interface, so no metadata lookup exists. The interface
check covers a binary union and a hand-written type that marks itself the same
way.

### E128219: Memberless abstract base is a closed hierarchy

Two-pass compilation-start and compilation-end analysis, the same shape as the
sealed-by-default rule E128005.

- Candidates: abstract classes, internal accessibility, every declaring
  syntax reference is a `TypeDeclarationSyntax` with no members, and no
  positional record parameter list. A positional record synthesizes members
  from its parameter list.
- Derivations: the base type of every named type, collected at symbol level.
- Report: compilation end, when at least one type derives from the base and
  every deriving type lives in the base assembly. A second assembly means the
  hierarchy is open, so the rule stays silent.
- The rule skips a base that already carries the `closed` modifier. The
  `closed` modifier alone implies abstract (`CS0144` on `new`), so a closed
  base is already the target shape.
- Code fix: replaces the `abstract` modifier token with the `closed` keyword
  token and keeps both trivia. The diagnostic carries
  `WellKnownDiagnosticTags.CompilationEnd`. The code fix test therefore sets
  `CodeFixTestBehaviors.SkipLocalDiagnosticCheck` and
  `NumberOfFixAllIterations = 1`.

### E128220: Union switch ends in a discard arm

Registers on `SwitchExpression` and `SwitchStatement`. The governing type is
the unwrapped receiver type, confirmed by `UnionTypes.IsUnion`.

- Catch-all shapes: `DiscardPatternSyntax` and `VarPatternSyntax`. The rule
  reports the last catch-all arm, unless a `null` constant pattern arm exists
  anywhere in the switch. A `null` arm names the empty case, so the switch
  keeps the missing-case warning.
- Under the union case-name syntax, `_` in a statement `case` label parses as
  a case-type identifier, not a discard. The statement catch-all that
  compiles is `case var _:`. The expression arm `_` is a discard.
- Code fix: replaces a discard pattern with a `null` constant pattern. A
  `var` arm binds a value, so the fixer skips it. The fixed state still
  carries the diagnostic, and the fix test passes it through
  `FixedState.ExpectedDiagnostics`.

### E128221: Union value read outside a pattern match

Registers on `SimpleMemberAccessExpression` and `CastExpression`.

- A member access named `Value` whose receiver is a union. The accessed
  member resolves to an `IPropertySymbol` on the containing type of the
  union, because a dictionary or a task carries a `Value`-shaped member too.
- A cast whose operand is a union and whose target type differs from the
  operand type.
- No code fix. Converting a read or a cast into a pattern or a `TryGetValue`
  call changes control flow.

### Test fixtures

The union source needs three settings in the test harness, because the
net10.0 reference assemblies predate the union runtime:

1. `LanguageVersion.Preview` on the parse options
2. A suppression for `RSEXPERIMENTAL006` on the compilation options
3. The `UnionTestRuntime.StubSource` file, which declares the
   `UnionAttribute`, the `IsClosedTypeAttribute`, and the `IUnion` interface
   that the compiler binds the synthesized shapes to

`case _` on a union governing type in a switch statement does not compile
(`CS0103`). Use `case var _:` for a statement fixture.
