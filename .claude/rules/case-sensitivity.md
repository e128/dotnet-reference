# Case-Sensitive Paths

macOS is case-insensitive by default. Linux CI (`ubuntu-24.04`, ext4) is
case-sensitive. **Always use the exact filesystem casing.**

This rule applies to:

- Hardcoded paths in C#: `Path.Combine()`, test data, embedded resources
- Solution file folder names and project references
- `using` directives for namespaces derived from the folder structure
- Git operations. `git mv` is case-aware. A plain rename is not.

**After any directory or file rename**, search for a stale reference:
`rg 'OldName' --type cs`.
