# Known Exceptions (codebase-specific)

Reference data for code-review agents. Patterns here are legitimate and should not be flagged as issues.

**Test conventions (project-wide):**
- Non-sealed `public class` test classes: CA1515 is relaxed for test code. Not a violation.
- Class-level `[Trait("Category", "CI")]` without per-method decoration: acceptable house style. Downgrade to LOW advisory.

**Roslyn navigator:**
- Severity mapping and generated-code handling: see `../../mr-sparkle-dotnet/references/dotnet-lens.md`.

## Adding New Exceptions

Format: add under the appropriate category heading. If no category fits, create a new one. Name only files that exist in this repo.
