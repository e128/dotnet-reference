# Domain-Specific Threat Patterns

Pre-built threat patterns keyed to this repo's projects and to generic .NET app archetypes.
When `/threat-model` targets one of these, seed Phase 2 with the listed surfaces to avoid
re-discovering known threats. Always confirm against the current code -- these are starting
points, not findings.

## E128.Reference.Web (Minimal-API Web App)

**Trust boundary profile:** Kestrel-hosted minimal API. Network-facing. Endpoints defined in
`Program.cs`. Treat every request path as crossing the client->application boundary.

### Known Attack Surfaces

| Surface                | Element Type    | Key Threats                                            |
| ---------------------- | --------------- | ------------------------------------------------------ |
| Minimal-API endpoints  | Process         | All six STRIDE categories apply                        |
| Request body / models  | Data Flow       | Injection, parameter tampering, mass assignment        |
| Route / query params   | Data Flow       | Tampering, IDOR, path traversal                        |
| Outbound HTTP calls    | Data Flow       | SSRF (CAPEC-310), counterfeit service (CAPEC-194)      |
| Error responses        | Data Flow       | Stack-trace / detail leakage (CAPEC-118)               |
| Response/output cache  | Data Store      | Cache poisoning (CAPEC-141)                            |
| Repository / DbContext | Data Store      | SQL injection if raw SQL, IDOR                         |

### Discovery Checklist

`rg "app\.Map(Get|Post|Put|Delete)" src/E128.Reference.Web -g "*.cs"` for endpoints;
`rg "Authorize|RequireAuthorization|AddAuthentication" src/` for auth gates;
`rg "ProblemDetails|DeveloperException|UseExceptionHandler" src/` for error handling posture.

## E128.Reference.Cli (System.CommandLine CLI)

**Trust boundary profile:** Local CLI. No network listeners. Attack surface is invocation
arguments, environment, and any files/processes it touches.

### Known Attack Surfaces

| Surface               | Element Type | Key Threats                                          |
| --------------------- | ------------ | ---------------------------------------------------- |
| Command arguments     | Data Flow    | Parameter injection (CAPEC-137) if passed to a shell |
| File path arguments   | Data Flow    | Path traversal, arbitrary file read/write            |
| External process spawn| Process      | Command injection via unsanitized args               |
| stdout / stderr        | Data Flow    | Info disclosure of secrets/paths in verbose output   |

### Discovery Checklist

`rg "Process\.Start|ProcessStartInfo" src/` for process launches;
`rg "Path\.Combine|File\.(Read|Write|Open)|Directory\." src/` for file I/O entry points.

## E128.Reference.Core (Class Library)

**Trust boundary profile:** No direct external entry point -- exercised by Web and CLI. Threats
are inherited from callers; focus on whether the library validates inputs at its public API
surface and avoids leaking sensitive data through exceptions or return values.

### Known Attack Surfaces

| Surface                  | Element Type | Key Threats                                   |
| ------------------------ | ------------ | --------------------------------------------- |
| Public service/repo APIs | Process      | Tampering via unvalidated inputs from callers |
| In-memory repositories   | Data Store   | Tampering, no integrity guarantees            |
| Exceptions thrown        | Data Flow    | Info disclosure if surfaced to clients        |

## E128.Analyzers (Roslyn Analyzer NuGet Package)

**Trust boundary profile:** A *build-time* component. The trust boundary is the **consumer's
compiler and IDE** -- the analyzer runs inside every consuming build, and its input is
arbitrary (potentially untrusted) source code. Threats here are about the package and the
analyzer's own robustness, not a runtime service.

### Known Attack Surfaces

| Surface                   | Element Type    | Key Threats                                          |
| ------------------------- | --------------- | ---------------------------------------------------- |
| Published NuGet package   | Data Store      | Supply-chain compromise (CAPEC-664), tampered package |
| Analyzer execution        | Process         | DoS: unbounded work / crash on malformed syntax tree |
| Code-fix providers        | Process         | Tampering: a buggy fix silently corrupts source      |
| Analyzer file I/O / network| Data Flow      | EoP: analyzers must not read files or call the network |

### Known/Expected Mitigations

| Mitigation                          | Threat Covered                       | Verify with                              |
| ----------------------------------- | ------------------------------------ | ---------------------------------------- |
| OIDC trusted publishing to nuget.org| Package-source tampering             | `lode/infrastructure/nuget-trusted-publishing.md` |
| `nuget.config` trusted signers      | Counterfeit dependency injection     | `nuget.config` `<trustedSigners>`        |
| CPM + transitive pinning            | Transitive dependency drift          | `lode/dotnet/project-structure.md`       |
| No file/network access in analyzers | EoP at build time                    | `rg "File\.|HttpClient|Socket" src/E128.Analyzers` |
| Renovate / `dep-check.sh`           | Vulnerable dependency intake         | `scripts/dep-check.sh --vulnerable`      |

## Generic .NET Archetypes (when no project matches)

If the target `domain` is not one of the above, classify it into one of these archetypes and
apply the matching surface set:

| Archetype                | Primary boundary           | Lead threats                                  |
| ------------------------ | -------------------------- | --------------------------------------------- |
| ASP.NET Core web/API     | Network (request)          | Auth bypass, injection, SSRF, IDOR, DoS       |
| Worker / background svc  | Queue/message input        | Poison messages, deserialization, DoS         |
| CLI / console tool       | Args + filesystem          | Param injection, path traversal               |
| Class library / SDK      | Public API of callers      | Input validation, info disclosure             |
| Build-time component     | Consumer build/IDE         | Supply chain, build DoS, EoP                  |

## Adding New Domains

When `/threat-model` runs against a domain not listed here and discovers significant
patterns (3+ threats), add a new section following this template:

```markdown
## {Domain Name} ({Archetype})

**Trust boundary profile:** {1-2 sentences: what it receives, stores, exposes}

### Known Attack Surfaces

| Surface | Element Type | Key Threats |
| ------- | ------------ | ----------- |

### Known Mitigations

| Mitigation | Threat Covered | Verify with |
| ---------- | -------------- | ----------- |
```
