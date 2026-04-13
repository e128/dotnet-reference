# Known Exceptions (codebase-specific)

Reference data for code-review agents. Patterns here are legitimate and should not be flagged as issues.

**Test conventions (project-wide):**
- Non-sealed `public class` test classes — CA1515 is relaxed for test code. Not a violation.
- Class-level `[Trait("Category", "CI")]` without per-method decoration — acceptable house style. Downgrade to LOW advisory.
- `PixelEnvySanitizerTests.cs` block namespace syntax — pre-existing style inconsistency. Note but do not block.

**Threat model (local CLI tool — no attacker-controlled input):**
- `SubstackSanitizer.cs` unbounded `NextElementSibling` traversal — trusted source, no attacker-controlled HTML. DoS concern is negligible.
- `PdfInfoCommand.cs` output path without `StartsWith` boundary check — CLI args from authenticated local user; `Path.GetFileNameWithoutExtension` strips directory components.

**Sanitizer TextContent/DOM rules:**
- `AtlanticSanitizer.cs` TextContent.Contains on div/section/p — all calls have `Length < N` guards (100–500). Compliant. Do not re-flag.
- `CondeNastSanitizer.cs` RemoveSignInDivs — `Length < 20` guard is tight. Downgrade to LOW advisory.
- `MacRumorsSanitizer.cs` / `AtlasObscuraSanitizer.cs` `RemoveContainerWithText` loop — iterates QSA without `.ToList()` but is read-only (no DOM mutation). Safe.
- `MediumSanitizer.cs` `RemoveBlockedUnblockText` with `maxTextLength: int.MaxValue` — intentional; matched string is a unique MHTML artifact, documented in XML doc. LOW advisory only.
- `SanitizerHelpers.RemoveByTextContent` default `maxTextLength=300` — calling without explicit arg is NOT a missing guard. The default parameter provides the guard.

**Sanitizer severity calibration rules:**
- Live-list: `QuerySelectorAll("[attr]")` + `RemoveAttribute(attr)` = CRITICAL. Tag/wildcard selector + `RemoveAttribute` = MEDIUM-convention. `SetAttribute` on attr-qualified selector = MEDIUM-convention.
- TextContent guard ordering: `Contains(x) && Length < N` and `Length < N && Contains(x)` are both CORRECT. Length-first is preferred (O(1) short-circuit). Guard in second position = MEDIUM perf advisory, NOT CRITICAL. Absent guards remain CRITICAL.

## Adding New Exceptions

Format: add under the appropriate category heading (test conventions, threat model, or sanitizer rules). If no category fits, create a new one.
