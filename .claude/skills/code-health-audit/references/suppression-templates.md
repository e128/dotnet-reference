# Remediation Templates

Templates for Step 5 (fix proposals) and Step 6 (planning output) of the suppression review workflow.

## Fix Proposal Template (Step 5)

For each suppression identified for remediation, propose a fix based on the
[Microsoft Code Analysis Rules reference](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/):

```
## [Rule ID] - [Rule Description]
**Current state**: Suppressed in [N] locations
**Why suppressed**: [Inferred reason from code context]
**Proposed fix**: [Specific code change or refactoring]
**Effort**: [Low/Medium/High]
**Files affected**:
- path/to/file1.cs:123
- path/to/file2.cs:456
```

**Example:**
```
## CA1822 - Mark members as static
**Current state**: Suppressed in 15 locations
**Why suppressed**: Methods don't use instance state but weren't marked static
**Proposed fix**: Add static keyword to methods that don't access instance members
**Effort**: Low (automated fix available)
**Files affected**:
- src/MyProject/Utilities.cs:45
- src/MyProject/Utilities.cs:89
- ... (13 more)
```

## Planning Output Template (Step 6)

Format findings for the dev-planning skill:

```markdown
# Code Suppression Remediation Plan

## Context
[1-2 paragraphs explaining the current state, how many suppressions exist,
and why addressing them matters]

## Goals
- Eliminate [X] suppressions by fixing root causes
- Move [Y] test-specific suppressions to editorconfig
- Document [Z] legitimate suppressions with rationale

## High Priority Fixes (Priority 1)
[List of fixes from Priority 1 with estimates]

## Medium Priority Fixes (Priority 2)
[List of fixes from Priority 2 with estimates]

## Editorconfig Changes
[Suppressions that should become editorconfig rules]

## Acceptable Suppressions
[Suppressions to keep with documented rationale]

## Success Criteria
- Build produces [N fewer] warnings
- No pragma suppressions without documented rationale
- Test projects use editorconfig for exceptions
```
