# Threat Model Report Format

Template for the Phase 4 report written to `plans/threat-model-{domain}/{domain}-register.md`.

```markdown
# Threat Model: {domain}
*Generated: {timestamp}*
*Scope: {scope or "full domain"}*

## Data Flow Diagram

{Mermaid DFD}

## Threat Register

### CRITICAL ({count})

| ID | Element | STRIDE | Description | DREAD | CAPEC | CWE | Mitigation | Status |
|----|---------|--------|-------------|-------|-------|-----|------------|--------|

### HIGH ({count})
...

### MEDIUM ({count})
...

### LOW ({count})
...

## Security Requirements

| Req ID | From Threat | Requirement | Implementation Notes |
|--------|-------------|-------------|---------------------|

## Existing Mitigations

| Mitigation | Location | Covers Threats |
|------------|----------|----------------|

## Delta (vs prior run)

| Category | Count |
|----------|-------|
| NEW      | N     |
| RESOLVED | N     |
| UNCHANGED| N     |
```
