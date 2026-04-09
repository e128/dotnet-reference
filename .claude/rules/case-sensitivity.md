# Case-Sensitive Paths

macOS is case-insensitive by default; Linux CI (`ubuntu-24.04`, ext4) is case-sensitive. **Always use exact filesystem casing.**

This applies to:
- Hardcoded paths in C# (`Path.Combine()`, test data, embedded resources)
- Solution file folder names and project references
- `using` directives referencing namespaces derived from folder structure
- Git operations (`git mv` is case-aware; plain rename is not)

**After any directory or file rename**, search for stale references: `rg 'OldName' --type cs`
