# STRIDE + CAPEC Reference

Framework reference for the threat-model skill. Contains the STRIDE per-element matrix,
canonical mitigations, STRIDE-to-CAPEC mechanism bridge, and DREAD-lite scoring guide.

## STRIDE Categories

| Category               | Security Property Violated | Definition                                              |
| ---------------------- | -------------------------- | ------------------------------------------------------- |
| Spoofing               | Authentication             | Impersonating another user or system component          |
| Tampering              | Integrity                  | Malicious modification of data in transit or at rest    |
| Repudiation            | Non-repudiation            | Denying an action when no audit trail can refute it     |
| Information Disclosure | Confidentiality            | Exposing data to unauthorized parties                   |
| Denial of Service      | Availability               | Preventing legitimate users from accessing a service    |
| Elevation of Privilege | Authorization              | Gaining access or permissions beyond what was granted   |

## Per-Element Applicability Matrix

Not all STRIDE categories apply to all DFD element types. This matrix filters
false positives -- don't ask "can someone spoof a database?"

| Element Type    | Spoofing | Tampering | Repudiation | Info Disclosure | DoS | EoP |
| --------------- | -------- | --------- | ----------- | --------------- | --- | --- |
| External Entity | x        |           | x           |                 |     |     |
| Process         | x        | x         | x           | x               | x   | x   |
| Data Store      |          | x         | x           | x               | x   |     |
| Data Flow       |          | x         |             | x               | x   |     |

## Canonical Mitigations by Category

| Category               | Mitigations                                                                    |
| ---------------------- | ------------------------------------------------------------------------------ |
| Spoofing               | MFA, mutual TLS, token binding, session management, certificate pinning        |
| Tampering              | HMAC/digital signatures, input validation, DB integrity constraints, TLS       |
| Repudiation            | Append-only audit logs, signed log entries, SIEM integration, tamper detection  |
| Information Disclosure | Encryption at rest + in transit, field-level ACL, data masking, need-to-know   |
| Denial of Service      | Rate limiting, circuit breakers, resource quotas, CDN, auto-scaling            |
| Elevation of Privilege | Least privilege, RBAC, input sanitization, sandboxing, JWT validation          |

## Existing-Mitigation Discovery (.NET)

This repo has no fixed security-primitive catalog -- discover what already exists before
scoring. When a threat maps to a mitigation found below, mark it MITIGATED and reference the
implementation. Run these greps against `src/` (or the resolved domain project):

| Looking for                  | Grep / script                                                   | STRIDE Category        |
| ---------------------------- | --------------------------------------------------------------- | ---------------------- |
| Auth / authorization gates   | `rg "\[Authorize\|RequireAuthorization\|AddAuthentication" src/` | Spoofing, EoP          |
| Input validation             | `rg "Validator\|\[Required\]\|ModelState\|FluentValidation" src/`| Tampering, EoP         |
| Safe outbound HTTP           | `rg "IHttpClientFactory\|AddHttpClient" src/`                    | Spoofing, DoS          |
| Parameterized data access    | `rg "DbParameter\|FromSqlInterpolated\|@p" src/`                 | Tampering              |
| Output encoding / anti-XSS   | `rg "HtmlEncoder\|JavaScriptEncoder\|antiforgery" src/`         | Tampering, Info Disc.  |
| Secrets handling             | `rg "IDataProtectionProvider\|UserSecrets\|KeyVault" src/`      | Info Disclosure        |
| Rate limiting / size caps    | `rg "AddRateLimiter\|RequestSizeLimit\|MaxRequestBodySize" src/` | DoS                    |
| Logging / audit              | `rg "ILogger\|Serilog\|AddOpenTelemetry" src/`                  | Repudiation            |
| Supply-chain controls        | `nuget.config` trustedSigners + `scripts/dep-check.sh --vulnerable` | Tampering (supply chain) |

If a `lode/{domain}/security.md` exists for the target domain, read it first for documented
primitives and cross-reference its claims against the code (code is the source of truth).

## STRIDE-to-CAPEC Mechanism Bridge

Source: SEI/CMU (Carnegie Mellon Software Engineering Institute).

| STRIDE Category        | CAPEC Mechanism of Attack              | Example CAPEC Patterns                      |
| ---------------------- | -------------------------------------- | ------------------------------------------- |
| Spoofing               | Engage in Deceptive Interactions       | CAPEC-151 (Identity Spoofing)               |
| Tampering              | Manipulate Data Structures             | CAPEC-66 (SQL Injection), CAPEC-86 (XSS)   |
| Repudiation            | Inject Unexpected Items                | CAPEC-93 (Log Injection)                    |
| Information Disclosure | Collect and Analyze Information         | CAPEC-118 (Data Leakage), CAPEC-497 (Sniffing) |
| Denial of Service      | Abuse Existing Functionality           | CAPEC-125 (Flooding), CAPEC-130 (Excess Alloc) |
| Elevation of Privilege | Subvert Access Control                 | CAPEC-122 (Privilege Abuse), CAPEC-233 (Escalation) |

**Usage:** After STRIDE identifies a threat category, look up the CAPEC mechanism, then
drill into specific patterns within that mechanism that match the system's technology
stack and element type.

## CAPEC Taxonomy Structure

CAPEC organizes 559 attack patterns across three axes:

**Abstraction levels:**
- **Meta** -- technology-agnostic attack class (architecture review)
- **Standard** -- specific attack technique (design/implementation review)
- **Detailed** -- platform-specific variant (pen testing)

**Domains of Attack:** Software, Hardware, Communications, Supply Chain, Social Engineering,
Physical Security.

**CAPEC-to-CWE linkage:** Each CAPEC pattern lists the CWE weakness IDs it exploits.
Traverse: CAPEC pattern -> CWE weakness -> code audit checklist.

## CAPEC Patterns Relevant to .NET Apps

Common patterns for .NET web APIs, CLI tools, libraries, and build-time (analyzer) components:

| CAPEC ID  | Name                          | Relevant To              | Prerequisites                  |
| --------- | ----------------------------- | ------------------------ | ------------------------------ |
| CAPEC-66  | SQL Injection                 | EF Core / ADO.NET stores | User input reaches raw SQL     |
| CAPEC-86  | XSS (Stored/Reflected)        | Web HTML/Razor output    | User content rendered          |
| CAPEC-115 | Authentication Bypass         | API/web endpoints        | Auth mechanism present         |
| CAPEC-118 | Data Leakage                  | Error/exception messages | Stack traces surfaced          |
| CAPEC-125 | Flooding                      | Web endpoints            | No rate limiting               |
| CAPEC-130 | Excessive Allocation          | Request/file processing  | Unbounded input size           |
| CAPEC-137 | Parameter Injection           | CLI arg / process launch | Unsanitized args to `Process`  |
| CAPEC-141 | Cache Poisoning               | Response/output caching  | Unkeyed/un-fingerprinted cache |
| CAPEC-151 | Identity Spoofing             | Session / token auth     | Weak or unbound tokens         |
| CAPEC-194 | Fake/Counterfeit Service      | Outbound HTTP deps       | DNS/TLS not validated          |
| CAPEC-219 | XML External Entity (XXE)     | XML parsing              | DTD processing enabled         |
| CAPEC-242 | Code Injection                | Dynamic/reflection paths | User input in code paths       |
| CAPEC-310 | SSRF                          | Server-side URL fetches  | User-controlled URLs           |
| CAPEC-586 | Object Injection              | Deserialization          | Untrusted data deserialized    |
| CAPEC-664 | Supply Chain Compromise       | NuGet packages           | Dependency on untrusted pkgs   |

## DREAD-Lite Scoring Guide

3-factor simplified DREAD. Score each factor 1-3, multiply for priority (1-27).

| Factor         | 1 (Low)                     | 2 (Medium)                    | 3 (High)                          |
| -------------- | --------------------------- | ----------------------------- | --------------------------------- |
| Damage         | Minor data quality issue    | PII exposure or data loss     | Full system compromise            |
| Affected Users | Single user or session      | Subset of users               | All users or system-wide          |
| Exploitability | Requires insider/local      | Requires authentication       | Unauthenticated, scriptable       |

**Priority bands:**

| Score  | Priority | Action                                          |
| ------ | -------- | ----------------------------------------------- |
| 18-27  | CRITICAL | Immediate remediation; block release            |
| 9-17   | HIGH     | Plan remediation within current sprint          |
| 4-8    | MEDIUM   | Track on roadmap; fix opportunistically         |
| 1-3    | LOW      | Accept risk or defer; document in threat register |

**Context adjustments by deployment shape:**
- Local-only tools (no network listeners): Exploitability ceiling is 2 (requires local access)
- CLI tools without auth: Spoofing/EoP threats cap at MEDIUM unless the tool processes
  untrusted input from the network or launches external processes
- Network-facing web APIs / services: full scoring applies -- unauthenticated request paths are
  the primary attack surface
- Tools that fetch or process untrusted external content: full scoring applies -- the external
  content IS the attack surface
- Build-time components (Roslyn analyzers, source generators, MSBuild tasks): the trust boundary
  is the *consumer's* build/IDE. Exploitability is HIGH if the package is published, since
  untrusted source code is the analyzer's input and the analyzer runs in every consumer's compiler
