# Arch-Scan -- HTML Companion Template

Reference for generating self-contained HTML companion files alongside markdown output.
Each perspective file (`{perspective}.md`) may optionally produce a `{perspective}.html`.

---

## When to Generate HTML

Generate HTML companion when the output contains:
- Mermaid diagrams that benefit from live rendering
- Tables wider than 80 columns (markdown tables wrap poorly)
- Per-component detail sections that benefit from collapsible `<details>`

The `dependency-map` perspective always generates HTML. Other perspectives generate HTML only when the output exceeds ~100 lines.

---

## Token-Efficiency Principles

The HTML file is both written and read by the LLM. Minimize token waste:

1. **All CSS in one `<style>` block** -- never inline styles. Fixed ~800 token overhead vs 15-40 tokens per inline element
2. **`<main>` wraps all content** -- semantic "skip to content" signal
3. **Semantic tags only** -- `<section>`, `<table>`, `<h2>`, `<ul>`, `<details>`. Never `<div>` wrappers
4. **No HTML comments inside `<main>`** -- they cost tokens with zero navigation value
5. **Short class names** -- `.v` not `.verified-status-indicator`. LLMs ignore class names

---

## Dracula Theme CSS

```css
:root{--bg:#282A36;--cur:#44475A;--fg:#F8F8F2;--cmt:#6272A4;--cy:#8BE9FD;--gn:#50FA7B;--or:#FFB86C;--pk:#FF79C6;--pr:#BD93F9;--rd:#FF5555;--yl:#F1FA8C}
body{font-family:system-ui,sans-serif;max-width:80ch;margin:2rem auto;padding:0 1rem;line-height:1.6;background:var(--bg);color:var(--fg)}
h1{color:var(--pk);margin-top:0}
h2{color:var(--pr);margin-top:2rem;border-bottom:1px solid var(--cur);padding-bottom:.3rem}
h3{color:var(--cy);margin-top:1.5rem}
a{color:var(--cy)} a:visited{color:var(--pr)}
strong{color:var(--or)}
code{font-family:ui-monospace,'JetBrains Mono',monospace;font-size:.9em;color:var(--gn);background:var(--cur);padding:.1em .3em;border-radius:3px}
pre{background:var(--cur);color:var(--fg);padding:1rem;overflow-x:auto;border-radius:6px;border-left:3px solid var(--pr)}
pre code{background:none;padding:0}
table{border-collapse:collapse;width:100%;margin:1rem 0}
th,td{border:1px solid var(--cmt);padding:.4rem .6rem;text-align:left}
th{background:var(--cur);color:var(--pk)}
tr:hover{background:rgba(68,71,90,.5)}
details{margin:.5rem 0;border:1px solid var(--cur);border-radius:4px;padding:.5rem}
summary{cursor:pointer;font-weight:600;color:var(--cy)}
aside{border-left:3px solid var(--or);padding:.5rem 1rem;margin:1rem 0;background:rgba(68,71,90,.3)}
blockquote{border-left:3px solid var(--cmt);padding:.5rem 1rem;margin:1rem 0;color:var(--cmt);font-style:italic}
hr{border:none;border-top:1px solid var(--cur);margin:2rem 0}
footer{margin-top:3rem;color:var(--cmt)}
.v{color:var(--gn)} .u{color:var(--or)} .c{color:var(--rd)}
::selection{background:var(--pr);color:var(--bg)}
pre.mermaid{cursor:zoom-in;position:relative}
pre.mermaid::after{content:'click to expand';position:absolute;top:.3rem;right:.5rem;font-size:.7rem;color:var(--cmt);pointer-events:none}
.diagram-overlay{display:none;position:fixed;inset:0;z-index:9999;background:var(--bg);overflow:auto;cursor:zoom-out;padding:2rem}
.diagram-overlay.active{display:flex;align-items:center;justify-content:center}
.diagram-overlay .mermaid{max-width:none;width:95vw}
.diagram-overlay .close-hint{position:fixed;top:1rem;right:1.5rem;color:var(--cmt);font-size:.85rem}
```

---

## Mermaid Live Rendering

Include this script block in `<head>`:

```html
<script type="module">
import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
mermaid.initialize({startOnLoad:true,theme:'dark'});
document.addEventListener('DOMContentLoaded',()=>{
  document.querySelectorAll('pre.mermaid').forEach(p=>p.setAttribute('data-src',p.textContent));
});
document.addEventListener('click',async e=>{
  const pre=e.target.closest('pre.mermaid');
  if(!pre||pre.closest('.diagram-overlay'))return;
  const ov=document.createElement('div');ov.className='diagram-overlay active';
  const h=document.createElement('span');h.className='close-hint';h.textContent='click anywhere or press Esc to close';
  const c=document.createElement('pre');c.className='mermaid';c.textContent=pre.getAttribute('data-src')||pre.textContent;
  ov.append(h,c);document.body.appendChild(ov);
  await mermaid.run({nodes:[c]});
  const close=()=>ov.remove();ov.addEventListener('click',close);
  const onKey=ev=>{if(ev.key==='Escape'){close();document.removeEventListener('keydown',onKey)}};
  document.addEventListener('keydown',onKey);
});
</script>
```

---

## Markdown-to-HTML Conversion Rules

| Markdown | HTML |
|----------|------|
| `# Heading` | `<h1>` |
| `## Heading` | `<h2>` |
| `### Heading` | `<h3>` |
| `**bold**` | `<strong>` |
| `` `code` `` | `<code>` |
| `> warning` | `<aside>` |
| `> citation` | `<blockquote>` |
| Markdown tables | `<table><thead><tbody>` |
| Mermaid code blocks | `<pre class="mermaid">` (raw syntax, no fences) |
| Bullet lists | `<ul><li>` |
| Per-component detail | `<details><summary>` (collapsed by default) |

---

## HTML Structure Rules

1. **Each per-component section in `<details><summary>`** -- keeps long pages navigable
2. **Mermaid diagrams** use `<pre class="mermaid">` (not markdown fenced blocks)
3. **Tables** use `<thead>` / `<tbody>` separation
4. **`<aside>`** for warnings (`> warning` in markdown)
5. **HTML is a companion, not a replacement.** Both files coexist. Markdown is the AI-editable source of truth; HTML is the browsable rendition
6. **File size:** HTML is larger than markdown. The 250-line content guideline applies to semantic content, not raw markup. Up to ~600 lines acceptable for complex scopes
7. **`<meta>` tags:** include `lode-updated` with ISO 8601 timestamp for freshness tracking
