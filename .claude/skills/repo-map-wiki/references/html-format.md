# Repo-Map Wiki -- HTML Output Format

Tufte Dracula HTML conventions for wiki output. This file is self-contained and
authoritative — the canonical skeleton below carries all CSS, JS, and Mermaid inline. It
covers wiki-specific needs (navigation bar, product metadata, multi-page cross-linking).

## Design Principles

- **Self-contained** -- each `.html` file works when double-clicked from Finder. No external CSS/JS/images.
- **Data-ink ratio approaching 1.0** -- every visual element carries information
- **Serif on dark** -- Palatino on `#282a36`
- **Color carries hierarchy, not weight** -- all headings weight 400, no bold headings
- **Tables beat cards** -- booktabs style: three horizontal rules only
- **No PowerPoint cognitive style** -- flowing prose with embedded figures

## Canonical HTML Skeleton

Every wiki HTML file uses this skeleton verbatim. Copy and fill in the `{placeholders}`:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{Page Title} -- {Product Area} Wiki</title>
  <meta name="wiki-product" content="{product-area}">
  <meta name="wiki-section" content="{overview|architecture|engineering|guides|reference}">
  <meta name="wiki-updated" content="{ISO 8601 UTC}">
  <style>
    /* Dracula-Tufte (muted) -- canonical palette */
    :root {
      --surface: #282a36; --surface-alt: #1e1f29; --on-surface: #f8f8f2;
      --label: #a9b1d6; --muted: #7a82a6; --rule: #7a82a6; --rule-light: #44475a;
      --link: #8fc9d9; --code-bg: #343746;
      --orange: #e0a878; --red: #e57373; --purple: #a98ed6;
      --pink: #e48bb7; --green: #7fc99a;
      --data-1: #8fc9d9; --data-2: #e48bb7; --data-3: #7fc99a; --data-4: #d8df8e;
    }
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: Palatino, "Palatino Linotype", "Book Antiqua", Georgia, serif; width: clamp(960px, 80vw, 1600px); margin: 0 auto; padding: 3rem 2rem; line-height: 1.6; font-size: 15pt; background: var(--surface); color: var(--on-surface); }
    h1 { font-weight: 400; font-size: 2rem; margin-bottom: 0.25rem; letter-spacing: -0.02em; color: var(--pink); }
    h2 { font-weight: 400; font-style: italic; font-size: 1.4rem; margin: 2.5rem 0 0.75rem; color: var(--purple); }
    h3 { font-weight: 400; font-style: italic; font-size: 1rem; color: var(--label); margin: 1.5rem 0 0.4rem; }
    p { margin: 0.5rem 0; }
    a { color: var(--link); text-decoration: underline; text-decoration-thickness: 0.05em; text-underline-offset: 0.15em; text-decoration-color: var(--muted); }
    a:hover { text-decoration-color: var(--link); }
    strong { font-weight: 600; color: var(--orange); }
    em { color: var(--muted); }
    code { font-family: ui-monospace, 'JetBrains Mono', 'Fira Code', monospace; font-size: max(0.85em, 12pt); color: var(--green); background: var(--code-bg); padding: .1em .3em; border-radius: 3px; }
    pre { background: var(--code-bg); color: var(--on-surface); padding: 1rem; overflow-x: auto; border-left: 3px solid var(--purple); border-radius: 6px; font-size: max(0.85em, 12pt); }
    pre code { background: none; padding: 0; color: var(--on-surface); }
    section { margin-bottom: 2rem; }
    table { border-top: 2px solid var(--rule); border-bottom: 2px solid var(--rule); border-collapse: collapse; width: 100%; margin: 0.5rem 0; font-size: max(0.88em, 12pt); }
    th { font-weight: 400; font-style: italic; text-align: left; padding: .4rem .6rem; color: var(--pink); cursor: pointer; user-select: none; border-bottom: 1px solid var(--muted); }
    th:hover { color: var(--on-surface); }
    th::after { content: ' \2195'; color: var(--rule-light); font-size: 0.7em; }
    td { border: none; padding: .35rem .6rem; vertical-align: top; font-variant-numeric: tabular-nums; }
    tbody tr:nth-child(even) td { background: var(--code-bg); }
    tr:hover td { background: rgba(68,71,90,0.5); }
    table.summary-table { border-top: 2px solid var(--rule); border-bottom: 2px solid var(--rule); border-collapse: collapse; margin: 0.5rem 0 2rem; font-size: max(0.95em, 12pt); }
    table.summary-table td { padding: 0.3rem 1.2rem 0.3rem 0; border: none; white-space: nowrap; }
    table.summary-table td.val { font-variant-numeric: tabular-nums; text-align: right; padding-right: 2rem; font-weight: 600; }
    table.summary-table td.lbl { color: var(--label); font-style: italic; }
    details { margin: .5rem 0; border: 1px solid var(--rule-light); border-radius: 4px; padding: .5rem; }
    summary { cursor: pointer; font-weight: 400; font-style: italic; color: var(--link); padding: 0.4rem 0; }
    summary:hover { color: var(--on-surface); }
    details[open] summary { margin-bottom: 0.5rem; }
    aside { border-left: 3px solid var(--orange); padding: .5rem 1rem; margin: 1.5rem 0; color: var(--label); font-size: max(0.9em, 12pt); background: rgba(68,71,90,0.3); }
    blockquote { border-left: 3px solid var(--muted); padding: .5rem 1rem; margin: 1.5rem 0; color: var(--muted); font-style: italic; }
    hr { border: none; border-top: 1px solid var(--rule-light); margin: 2.5rem 0; }
    dl { margin: 1rem 0; }
    dt { font-weight: 600; margin-top: .5rem; }
    dd { margin-left: 1.5rem; margin-bottom: .5rem; color: var(--label); }
    ul { list-style: none; padding-left: 0; }
    ol { padding-left: 1.5rem; list-style: decimal; }
    li { padding: 0.15rem 0; font-size: max(0.88em, 12pt); line-height: 1.5; }
    nav { margin: 1rem 0; padding: .5rem 0; color: var(--muted); font-size: max(0.9em, 12pt); }
    nav.wiki-nav { display: flex; gap: 1rem; flex-wrap: wrap; border-bottom: 1px solid var(--rule-light); margin-bottom: 1.5rem; }
    footer { margin-top: 3rem; color: var(--muted); font-size: max(0.85em, 12pt); }
    .verified { color: var(--green); }
    .unverified { color: var(--orange); }
    .correction { color: var(--red); }
    .mermaid-overlay { position: fixed; inset: 0; z-index: 1000; background: rgba(30,31,41,0.92); display: flex; align-items: center; justify-content: center; cursor: zoom-out; opacity: 0; transition: opacity 0.2s; pointer-events: none; }
    .mermaid-overlay.active { opacity: 1; pointer-events: auto; }
    .mermaid-overlay svg { max-width: 95vw; max-height: 95vh; filter: drop-shadow(0 0 24px rgba(0,0,0,0.5)); }
    pre.mermaid { cursor: zoom-in; }
    ::selection { background: var(--purple); color: var(--surface); }
    @media (max-width: 600px) { body { width: auto; min-width: 0; padding: 1.5rem 1rem; } }
    @media print { body { background: #fff; color: #111; width: auto; max-width: none; padding: 1rem; } a { color: inherit; text-decoration: none; } h2 { page-break-after: avoid; } }
  </style>
</head>
<body>
<div class="mermaid-overlay" id="mermaid-zoom"></div>
<nav class="wiki-nav">
  <a href="index.html">Index</a> ·
  <a href="overview/executive.html">Executive</a> ·
  <a href="architecture/system.html">Architecture</a> ·
  <a href="engineering/getting-started.html">Engineering</a> ·
  <a href="reference/glossary.html">Reference</a>
</nav>
<header>
  <h1>{Page Title}</h1>
  <p><em>{One-sentence summary}</em></p>
</header>
<main>
  <!-- Content sections here -->
</main>
<footer>
  <hr>
  <p>Updated: <time datetime="{ISO 8601 UTC}">{ISO 8601 UTC}</time> -- Generated by repo-map-wiki</p>
</footer>
<script type="module">
  import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
  mermaid.initialize({ startOnLoad: true, theme: 'dark' });
  const overlay = document.getElementById('mermaid-zoom');
  const dismiss = () => { overlay.classList.remove('active'); overlay.innerHTML = ''; };
  document.querySelectorAll('pre.mermaid').forEach(pre => {
    new MutationObserver(() => {
      const svg = pre.querySelector('svg');
      if (svg) svg.addEventListener('click', () => {
        overlay.innerHTML = ''; overlay.appendChild(svg.cloneNode(true));
        overlay.classList.add('active');
      });
    }).observe(pre, { childList: true });
  });
  overlay.addEventListener('click', dismiss);
  document.addEventListener('keydown', e => { if (e.key === 'Escape') dismiss(); });
</script>
<script>
  /* Table sorting -- click th[data-col] to sort */
  document.querySelectorAll('th[data-col]').forEach(th => {
    th.addEventListener('click', () => {
      const table = th.closest('table'), tbody = table.querySelector('tbody');
      const col = +th.dataset.col, rows = [...tbody.rows];
      const dir = th.dataset.dir === 'asc' ? 'desc' : 'asc';
      table.querySelectorAll('th').forEach(h => delete h.dataset.dir);
      th.dataset.dir = dir;
      rows.sort((a, b) => {
        const av = a.cells[col]?.textContent.trim() ?? '';
        const bv = b.cells[col]?.textContent.trim() ?? '';
        const an = parseFloat(av), bn = parseFloat(bv);
        const cmp = (!isNaN(an) && !isNaN(bn)) ? an - bn : av.localeCompare(bv);
        return dir === 'asc' ? cmp : -cmp;
      });
      rows.forEach(r => tbody.appendChild(r));
    });
  });
</script>
</body>
</html>
```

## Semantic Color Roles

Each Dracula accent has exactly one semantic role. Never mix these:

| Accent   | CSS var      | Role                                          |
| -------- | ------------ | --------------------------------------------- |
| Pink     | `--pink`     | H1 heading, `<th>` text                      |
| Purple   | `--purple`   | H2 heading, `<pre>` border, selection bg      |
| Green    | `--green`    | Inline `<code>`, `.verified` status           |
| Orange   | `--orange`   | `<strong>`, `<aside>` border, `.unverified`   |
| Cyan     | `--link`     | Links, `<summary>` text                      |
| Red      | `--red`      | `.correction` status                          |
| Label    | `--label`    | H3 heading, `<dd>` text, secondary text       |

## HTML Tag Usage

| Purpose                | Tag                                        | Notes                              |
| ---------------------- | ------------------------------------------ | ---------------------------------- |
| Key metrics            | `<table class="summary-table">`            | `lbl`/`val` cells, up to 4/row    |
| Sortable data tables   | `<table>` with `<th data-col="N">`         | Booktabs, click-to-sort headers    |
| Collapsible detail     | `<details><summary>...</summary>...</details>` | Lengthy code, deep-dive sections |
| Mermaid diagrams       | `<pre class="mermaid">`                    | Client-side render, click-to-zoom  |
| Code blocks            | `<pre><code>...</code></pre>`              | Wrap in `<details>` if >15 lines   |
| Inline code            | `<code>`                                   | Green on dark -- names, paths      |
| Callout / warning      | `<aside>`                                  | Orange border, semi-transparent bg |
| Definition lists       | `<dl><dt>...<dd>...`                       | Glossaries, parameter docs         |
| Timestamps             | `<time datetime="">`                       | ISO 8601 UTC                       |
| Status indicators      | `<span class="verified|unverified|correction">` | Semantic status classes      |
| Block quotes           | `<blockquote>`                             | Quoting external sources           |
| Grouped content        | `<section>`                                | Wrap each `<h2>` and its content   |

### Summary Card Tables

```html
<table class="summary-table">
  <tr><td class="lbl">Repos</td><td class="val">12</td>
      <td class="lbl">Controllers</td><td class="val">47</td></tr>
</table>
```

### Sortable Data Tables

```html
<table>
  <thead><tr><th data-col="0">Project</th><th data-col="1">Framework</th></tr></thead>
  <tbody><tr><td>MyApp.Core</td><td>.NET 10</td></tr></tbody>
</table>
```

### Mermaid Diagrams

Use `<pre class="mermaid">` blocks (NOT markdown fenced code blocks). Mermaid.js renders
client-side via ESM module import (included in the skeleton). Click any rendered diagram
to zoom fullscreen; press Escape or click the overlay to dismiss.

```html
<pre class="mermaid">
graph TB
  A[Admin API] --> B[Workflow Service]
  B --> C[Job Scheduler]
</pre>
```

## Wiki-Specific Conventions

### Navigation Bar

Every file includes `<nav class="wiki-nav">` with links to major sections. Adjust relative
paths based on file depth:

- **Root files** (`docs/index.html`): `href="overview/executive.html"`
- **Subdirectory files** (`docs/architecture/system.html`): `href="../index.html"`, `href="../overview/executive.html"`

### Metadata Tags

Three `<meta>` tags in `<head>` enable programmatic discovery:

- `wiki-product` -- the product area name (e.g., "assure", "iga")
- `wiki-section` -- which tree section (overview, architecture, engineering, guides, reference)
- `wiki-updated` -- ISO 8601 UTC timestamp of last generation

### File Links

All internal links use `.html` extensions and relative paths. Never use absolute paths or
markdown-style `.md` references in HTML output.
