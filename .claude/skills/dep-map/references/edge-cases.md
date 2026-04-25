# dep-map — Edge Cases

Reference for handling unusual repo structures during Step 3 scanning.

---

| Scenario | Handling |
| -------- | -------- |
| **Single-repo scan root** — scan root itself contains manifests | Treat as single-repo run. Scope label = section heading; per-repo header = directory name. |
| **Empty dir / no manifests** | Include in summary table with `⚠ no manifests found`. Skip detailed scanning. |
| **Non-service repos** — names contain `sql`, `infra`, `terraform`, `performance`, `qa`, `automation`, `design`, `release`, `scripts`, `common` | Include in summary table with type noted. Draw minimal single-node diagram or note "No running services — infrastructure/tooling repo." |
| **Multiple .csproj per repo** | Aggregate all non-test `.csproj`. De-duplicate across files. Note project count: `**Projects:** 3 (.csproj files, tests excluded)`. |
| **Large dependency list (> 30 packages)** | List only architecturally significant packages. Add footnote: `> N additional packages omitted. Full list: <relative path to manifest>.` |
| **Maven multi-module repos** | Read root `pom.xml` + first-level sub-module POMs. Skip deeply nested test sub-modules. |
| **package.json in Angular/React monorepo** | Read root `package.json` + main application `package.json`. Skip library sub-projects. |
| **No Dockerfile** | Mark runtime image as `—`. Note: `No container image — library/tooling repo.` |
| **Multi-stage Dockerfile** | Use the final stage (last `FROM` without `AS`, or named `final`/`release`/`production`/`runtime`) as the production image. List build stages in SDK/build column. |
| **Dockerfile with no tag (`:latest` or no tag)** | Note as `⚠ unpinned`. Flag in report summary. |
| **global.json rollForward policy** | Record `sdk.version` + `rollForward`. `patch`/`latestPatch` = safe; `major`/`latestMajor` = permissive, flag it. |
| **CI SDK version differs from global.json** | Record both values with `⚠ mismatch` note. |
