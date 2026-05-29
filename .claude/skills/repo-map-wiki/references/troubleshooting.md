# Repo-Map Wiki: Troubleshooting

- **Repo is too large to explore in one agent** -- Split investigation across multiple parallel agents by directory (e.g., API/, UI/, Infrastructure/)
- **No CLAUDE.md or README in repo** -- Rely on solution/project files, Startup.cs, and directory structure. Read package.json for frontend repos.
- **Mixed tech stacks** -- Create separate service docs per tech (e.g., separate docs for .NET API and Angular UI even if in same repo)
- **Monorepo with many sub-projects** -- Focus docs on logical service boundaries, not individual projects. One doc per deployable unit.
- **Mermaid diagrams not rendering** -- Ensure `<pre class="mermaid">` tags are used (not fenced code blocks). Check that the Mermaid CDN script loads. For offline use, download `mermaid.min.js` into `docs/` and reference locally.
- **CSS looks wrong** -- Re-read the source CSS files; they may have changed since last generation. Never hardcode theme values.
- **Agent returns empty** -- Resume with SendMessage providing specific follow-up questions
- **Existing docs/ with different conventions** -- Respect the existing structure; do not overwrite. Use the convention profile detection to adapt.
