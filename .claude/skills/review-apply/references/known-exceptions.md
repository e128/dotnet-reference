# Known Exceptions (codebase-specific)

Reference data for code-review agents. Patterns here are legitimate and should not be flagged as issues.

**Test conventions (project-wide):**
- Non-sealed `public class` test classes: CA1515 is relaxed for test code. Not a violation.
- Class-level `[Trait("Category", "CI")]` without per-method decoration: acceptable house style. Downgrade to LOW advisory.

**Roslyn navigator expectations (--full mode):**
- `detect_antipatterns` may report patterns in generated code (`*.Designer.cs`, `*.g.cs`), exclude from findings.
- `detect_circular_dependencies` reports at project/namespace level: map to MEDIUM if cycle is within a single project (namespace-level), HIGH if cross-project.

## Adding New Exceptions

Format: add under the appropriate category heading. If no category fits, create a new one. Name only files that exist in this repo.
