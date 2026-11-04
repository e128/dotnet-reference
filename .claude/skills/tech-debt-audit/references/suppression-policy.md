# Suppression Policy

Reference material for the `suppressions` mode of tech-debt-audit.

## Suppression Policy (Active)

**Suppressions are a last resort, not a first response.**

```
When I see: Analyzer warning
I should: Investigate and propose fix
I should NOT: Add #pragma warning disable
Exception: Only after explicit human approval with documented reason
```

## Notes

- The scanner script is read-only and safe to run repeatedly
- Test the scanner on a small directory first to verify output format
- Review findings with the team before mass-applying fixes
- Consider running scanner in CI to prevent new suppressions
