# Suppression Policy & Examples

Reference material for the code suppression reviewer skill.

## Suppression Policy (Active)

**Suppressions are a last resort, not a first response.**

```
When I see: Analyzer warning
I should: Investigate and propose fix
I should NOT: Add #pragma warning disable
Exception: Only after explicit human approval with documented reason
```

## Examples

### Example 1: Direct Invocation

**User**: `/code-suppression-reviewer`

**Claude**:
1. Runs scanner script on repo
2. Generates report
3. Analyzes findings using Microsoft Code Analysis Rules reference
4. Proposes fixes categorized by priority
5. Generates dev-planning compatible output
6. Updates memory about suppression policy

### Example 2: Discovering a Suppression

**User**: "Why is CA1822 suppressed in this file?"

**Claude**:
1. Reads the suppression context
2. Looks up CA1822 in Microsoft docs
3. Explains: "CA1822 suggests marking members as static when they don't use instance state"
4. Proposes: "These methods can be marked static, which enables better performance and clearer intent"
5. Asks: "Should I fix this instead of suppressing?"

### Example 3: Preventing New Suppressions

**User**: "This CA2007 warning is annoying, just suppress it"

**Claude**:
1. Looks up CA2007: it's about ConfigureAwait
2. Explains the risk: "CA2007 prevents potential deadlocks in library code"
3. Proposes fix: "Add .ConfigureAwait(false) to the await"
4. Only suppresses if user insists after understanding the implications

## Anti-Patterns to Avoid

**Don't do this:**
```csharp
#pragma warning disable CA1062 // Validate arguments
public void Process(string input)
{
    // I'll fix this later
    var result = input.ToUpper();
}
#pragma warning restore CA1062
```

**Do this:**
```csharp
public void Process(string input)
{
    ArgumentNullException.ThrowIfNull(input);
    var result = input.ToUpper();
}
```

## Success Metrics

A successful suppression review should result in:

1. **Fewer suppressions** - Measurable reduction in pragma count
2. **Documented remainders** - All kept suppressions have clear rationale
3. **Consistent policy** - Project memory reflects fix-first approach
4. **Actionable plan** - Clear steps for remediation integrated with project planning

## Notes

- The scanner script is read-only and safe to run repeatedly
- Test the scanner on a small directory first to verify output format
- Review findings with the team before mass-applying fixes
- Consider running scanner in CI to prevent new suppressions
