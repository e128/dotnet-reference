# Step 8: Cleanup & Organization

**Executes immediately after Steps 3–7 fixes are complete. No findings table, no approval gate.**
These are mechanical, non-behavioral changes. Report what was changed at the end.

---

## 8a. Sort and Organize Directory.Packages.props

Read `Directory.Packages.props` and reorganize:

1. **Sort `<PackageVersion>` entries alphabetically** by `Include` attribute within each `<ItemGroup>`
2. **Group by purpose** if there are many packages (>20), using XML comments:
   ```xml
   <ItemGroup>
     <!-- Build & Analysis -->
     <PackageVersion Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="10.0.102" />

     <!-- Microsoft.Extensions -->
     <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />

     <!-- Testing -->
     <PackageVersion Include="xunit" Version="2.9.0" />
   </ItemGroup>
   ```
3. **Remove duplicate entries** — if the same package appears twice, keep the higher version
4. **Remove orphaned entries** — packages not referenced by any `.csproj` in the solution
5. **Consistent formatting** — align `Version` attributes, consistent quote style, no trailing whitespace

---

## 8b. Sort .editorconfig Rules

If the root `.editorconfig` has analyzer rules:

1. **Group `dotnet_diagnostic.*` rules by CA category:**
   - Design (CA1xxx), Maintainability (CA15xx), Naming (CA17xx), Performance (CA18xx)
   - Reliability (CA20xx), Security (CA2xxx/CA3xxx/CA5xxx), Usage (CA22xx)
2. **Sort rules numerically within each group**
3. **Add section comments** for each category if not present

---

## 8c. Review .editorconfig Suppressions

Audit all `.editorconfig` files for stale or overly broad suppressions.
The overhaul may have fixed issues that originally motivated a suppression.

**Root `.editorconfig`:**
1. Find all `dotnet_diagnostic.*.severity = none` or `= silent` entries
2. For each suppression, check whether the underlying issue still exists:
   - Grep for the pattern the rule detects
   - If the overhaul fixed all instances → **remove the suppression**
   - If instances remain but were intentionally deferred → **leave it** and add: `# Deferred: [finding ID]`
3. Flag blanket category suppressions (`dotnet_analyzer_diagnostic.category-*.severity = none`) —
   almost always too broad; replace with individual rule suppressions
4. Remove duplicate rules (same ID with conflicting severity in different sections — keep most specific)

**Child `.editorconfig` files (test projects):**
These expected suppressions should NOT be removed:

| Rule | Why it's suppressed in tests |
|------|------------------------------|
| CA1707 | Underscores in test method names |
| CA1515 | Public test classes (test runners require public) |
| CA1062 | Null guards not needed in test methods |
| CA1812 | Instantiated by test framework, not directly |
| CA2007 / MA0004 / VSTHRD111 | `ConfigureAwait(false)` not needed in test context |
| VSTHRD200 | `Async` suffix not conventional in tests |
| CA1034 | Nested public types for test organization |

However, check test `.editorconfig` files for suppressions beyond this expected set:
- Rules unrelated to test conventions — may be hiding real issues in test helper code
- Blanket `dotnet_analyzer_diagnostic.severity = none` in test projects (too broad)
- Suppressions for rules the overhaul already fixed — remove if no longer needed

**Report changes:** List each suppression removed or narrowed with the reason.

---

## 8d. Verify Build

```bash
${CLAUDE_SKILL_DIR}/scripts/build.sh <solution> --json
```

These are formatting-only changes — the build must still pass. If it doesn't, revert the specific
cleanup change that broke it and report the issue.
