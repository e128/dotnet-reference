# Arch-Scan Output Template

For each perspective, write to `lode/{domain}/architecture/{perspective}.md`:

```markdown
# {Domain} -- {Perspective Title}
*Generated: {ISO 8601 UTC timestamp}*
*Scope: {source directories}*

---

## Overview Diagram

\```mermaid
{top-level diagram}
\```

---

## Components

### {Element 1 -- Group}

\```mermaid
{drill-down sub-diagram}
\```

**Description:** ...
**Context:** ...

#### {Child Element 1a -- Leaf}
**Description:** ...
**Context:** ...

### {Element 2 -- Leaf}
**Description:** ...
**Context:** ...
```

## HTML Companion (dependency-map perspective)

For the `dependency-map` perspective (and any other perspective exceeding ~100 lines), generate
an HTML companion file alongside the markdown:

Write to `lode/{domain}/architecture/{perspective}.html`.

The HTML file renders the same content as the markdown file but as a self-contained
Dracula-themed document with:
- Live Mermaid diagram rendering (CDN-loaded)
- Click-to-expand diagram overlay
- Collapsible `<details>` for per-component sections
- Dark theme optimized for readability

See [html-companion.md](html-companion.md) for the full template, CSS, Mermaid script, and
token-efficiency principles.

**Key rule:** The HTML is a companion, not a replacement. Both files coexist. Markdown is the
AI-editable source of truth.
