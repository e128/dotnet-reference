# Analyzer Decisions: Helpers
*Updated: 2026-11-20T17:16:51Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## UnionSwitchDiscardArmAnalyzer
- Applies the same catch-all rule to a closed class hierarchy because it carries the compiler closed attribute. A named or null arm keeps CS8509 alive for a missing leaf.
- Reports a when-guarded discard or var arm because it never closes the switch. Only an unguarded arm closes it.

## UnionValueReadAnalyzer
- Confirms the accessed member resolves on the union itself. This holds because a dictionary or task also carries a Value-shaped member.

## UnionTypes
- Matches the union interface by name and namespace chain because the compiler synthesizes the interface. As a result, no metadata name resolves it.

## ExclusiveBranches
- ExclusiveBranches.Are treats nodes in different switch sections as exclusive because an empty section falls through and holds no enumeration. As a result, only non-null sections in different sections qualify.

## InModifierHelper
- Follows the modifier order in the C# language reference. A modifier placed out of order still compiles. As a result, only a reader notices the defect.

## LoopContext
- Refuses a hoist when a call inside the lookup can return a different value per iteration. This holds because the hoist would change behavior.
- Treats a node in a loop header as running once per outer iteration. As a result, the outer loop decides whether a hoist is safe.

## TrackerReference
- Compiles one Regex per configured pattern string into a thread-safe cache. This holds because the rules run concurrently, and it holds one entry per distinct option value.
- Falls back to the default pattern for an invalid configured pattern instead of failing the build.
- Still reports an unmatched reference. As a result, a slow pattern cannot disable the rule.

Related: [Decision Records](summary.md) explains how to use this page.
