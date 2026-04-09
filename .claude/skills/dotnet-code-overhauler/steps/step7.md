# Step 7: Security Review

Two parallel parts: code security (Explore agent with patterns) + supply chain (Explore agent).

---

## Code Security

Launch an `Explore` agent (haiku) with patterns from `step7-patterns.md`:

```
Read .claude/skills/dotnet-code-overhauler/steps/step7-patterns.md for grep patterns and checklist.
Read lode/coding-standards.md (if it exists) for project coding conventions.
Read lode/coding-standards-async.md (if it exists) for async patterns.

Audit the code in [scope] for security vulnerabilities.
Run the anti-pattern grep patterns and analysis checklist against all .cs files in scope.
For each match, read surrounding context to confirm it's a real vulnerability (not mitigated).

Skip ASP.NET-specific checklist items (#11-14, #19) unless target code is explicitly web-facing.

Do NOT apply fixes — report findings only with severity ratings, CA rule references,
and file:line references.
```

Covers: SQL/command/XPath/LDAP injection, XSS, path traversal, hardcoded secrets,
weak cryptography, insecure randomness, insecure deserialization (BinaryFormatter, TypeNameHandling),
ASP.NET middleware ordering, cookie settings, CORS, antiforgery, HTTPS enforcement, token validation,
XML processing, and error information disclosure. Maps all findings to CA rules.

---

## Supply Chain

Launch a separate `Explore` agent (haiku) in parallel:

```
Glob GitHub Actions workflow files: .github/workflows/*.yml, .github/workflows/*.yaml

For each workflow file:
- Actions using mutable version tags (e.g., `uses: actions/checkout@v4`) instead of commit SHA pins.
  Safe form: `uses: actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11  # v4`
  All `uses:` references to external actions MUST be pinned to a full 40-character commit SHA.
- Container images in `container:` or `services:` blocks using floating tags (`:latest`, `-latest`, or no tag).
  Safe form: `image: mcr.microsoft.com/dotnet/sdk:10.0@sha256:<digest>`
- Third-party actions (not under `actions/` org) are highest risk — flag first.

Glob Dockerfiles: **/Dockerfile*, **/docker-compose*.yml

For each Dockerfile/compose file:
- FROM using floating tags (`:latest`, `-latest`, or no tag).
- FROM using a specific version tag without a digest pin.
  Safe form: `FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:<digest>`
  Note: Step 3 checks digest freshness; this step checks whether pins EXIST at all.
- docker-compose service images with floating or undigested tags.

For each docker-compose.yml, also check runtime security hardening:
- Missing `read_only: true` — filesystem should be immutable at runtime
- Missing `security_opt: no-new-privileges:true` — prevents privilege escalation via setuid/setgid
- Missing `cap_drop: ALL` — all Linux capabilities should be dropped
- Missing resource limits (`mem_limit`, `cpus`, `pids_limit`) — prevents resource exhaustion
- Missing `tmpfs` mounts for `/tmp` when `read_only: true` — app may need writable temp
- Exposed ports not matching app config (e.g., EXPOSE vs ASPNETCORE_URLS mismatch)

For each Dockerfile, also check:
- Missing `USER` instruction (non-root) — .NET images provide `$APP_UID`
- Package manager not removed after use (apk/apt left in runtime image)
- HEALTHCHECK using tools not in the base image (curl in Alpine, wget in Debian-slim)
```

---

## Findings Table

Merge specialist output and supply chain results into one table. **ID prefix: `S`**

| ID | Finding | Severity | Category |
|----|---------|----------|----------|
| S1 | SQL concatenation in UserRepository.cs:45 (CA3001) | CRITICAL | SQL injection |
| S2 | BinaryFormatter in LegacyImporter.cs:78 (CA2300) | CRITICAL | Insecure deserialization |
| S3 | API key hardcoded in AppSettings.cs:12 (CA5390) | HIGH | Secrets |
| S4 | Path.Combine with user input, no traversal check at ImportService.cs:78 (CA3003) | HIGH | Path traversal |
| S5 | MD5 used for password hashing in IdentityManager.cs:33 (CA5351) | HIGH | Weak crypto |
| S6 | `actions/checkout@v4` in ci.yml:12 — not pinned to commit SHA | HIGH | Supply chain |
| S7 | `coverallsapp/github-action@v2` in ci.yml:45 — not pinned to commit SHA | HIGH | Supply chain |
| S8 | Dockerfile FROM `dotnet/sdk:10.0` without digest pin | MEDIUM | Supply chain |

**Severity:**
- CRITICAL: Remote code execution, authentication bypass, data breach (specialist output)
- HIGH: Exploitable under realistic conditions; GitHub Actions using mutable tags (supply chain)
- MEDIUM: Defense-in-depth gap — overly permissive config, Dockerfile FROM without digest
- LOW: Hardening opportunity — HTTP for internal calls, verbose error messages
- INFO: Acknowledged risk with documented mitigation

Present the merged table. Wait for approval. Then Fix Cycle.
