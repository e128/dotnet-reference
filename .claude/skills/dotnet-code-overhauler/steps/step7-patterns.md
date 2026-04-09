# Security Review — Grep Patterns & Checklist

Reference file for Step 7 of dotnet-code-overhauler. Contains anti-pattern grep patterns
and analysis checklist previously embedded in the `dotnet-security-specialist` agent.

## Anti-Pattern Grep Patterns

```
# === INJECTION ===

# SQL injection (string concat/interpolation in queries)
\$".*SELECT|INSERT|UPDATE|DELETE.*\{    # interpolated SQL
"\s*\+\s*.*SELECT|INSERT|UPDATE|DELETE   # concatenated SQL
\.CommandText\s*=\s*\$"                  # interpolated command text
\.CommandText\s*=.*\+                    # concatenated command text
string\.Format\(.*SELECT|INSERT|UPDATE|DELETE  # String.Format SQL
\.FromSqlRaw\(\$"                        # EF Core raw SQL with interpolation
\.ExecuteSqlRaw\(\$"                     # EF Core execute with interpolation

# XSS (unencoded output)
@Html\.Raw\(                             # Razor unencoded output
\.Write\(.*Request\[                     # Writing request data directly
HtmlString\(.*\+                         # Building HTML from concatenation
Content\(.*Request\.                     # Returning request data as content

# Command injection
Process\.Start\(.*\+                     # Process start with concatenated args
ProcessStartInfo.*Arguments.*\+          # Concatenated process arguments
ProcessStartInfo.*FileName.*\$"          # Interpolated process filename

# Path traversal
Path\.Combine\(.*Request\.|input|param   # User input in file paths
new\s+FileStream\(.*\+                   # File access with concatenation
File\.(Read|Write|Open|Delete|Copy|Move).*\+  # File operations with concatenation
\.\.[\\/]                                # Directory traversal literals

# LDAP injection
DirectorySearcher.*Filter.*\+            # Concatenated LDAP filter
\.Filter\s*=.*\$"                        # Interpolated LDAP filter

# XPath injection
\.SelectNodes?\(.*\+                     # Concatenated XPath
XPathExpression.*\+                      # Concatenated XPath expression

# Regex injection (ReDoS)
new\s+Regex\(.*\+                        # Regex from user input
Regex\.(Match|Replace|IsMatch)\(.*,\s*\w+\s*[+$]  # Pattern from variable

# === CRYPTOGRAPHY & SECRETS ===

# Weak/broken algorithms (CA5350, CA5351)
MD5\.Create|\.ComputeHash.*MD5           # MD5 usage
SHA1\.Create|HMACSHA1                    # SHA1 usage
DES\.Create|DESCryptoServiceProvider     # DES usage
TripleDES\.Create|TripleDESCryptoServiceProvider  # 3DES usage
RC2\.Create|RC2CryptoServiceProvider     # RC2 usage
DSA\.Create|DSACryptoServiceProvider     # DSA (CA5384)

# Hardcoded secrets (CA5390, CA5403)
(password|pwd|secret|apikey|api_key|token|connectionstring)\s*=\s*"[^"]{8,}
new\s+byte\[\]\s*\{.*\d+.*\d+.*\}.*[Kk]ey|[Ii][Vv]  # Hardcoded key/IV bytes
Convert\.FromBase64String\("             # Base64 hardcoded value

# Insecure randomness (CA5394)
new\s+Random\(                           # System.Random (not cryptographic)
Random\.Shared                           # System.Random.Shared

# Insufficient key size (CA5385)
RSA\.Create\(\d{1,3}\)                   # RSA key < 2048
new\s+RSACryptoServiceProvider\(\d{1,3}\)

# Deprecated/hardcoded protocols (CA5364, CA5386, CA5397, CA5398)
SslProtocols\.Ssl|SslProtocols\.Tls\b   # SSLv3 or TLS 1.0
SecurityProtocolType\.Ssl|SecurityProtocolType\.Tls\b
ServicePointManager\.SecurityProtocol\s*=

# Disabled certificate validation (CA5359)
ServerCertificateValidationCallback\s*=
ServerCertificateCustomValidationCallback
DangerousAcceptAnyServerCertificateValidator

# Weak KDF (CA5379, CA5387, CA5388)
new\s+Rfc2898DeriveBytes\(.*,\s*\d{1,4}\)  # PBKDF2 < 100000 iterations
PasswordDeriveBytes                      # Obsolete KDF (CA5373)

# === INSECURE DESERIALIZATION ===

# Dangerous deserializers (CA2300-CA2330)
BinaryFormatter
NetDataContractSerializer
LosFormatter
ObjectStateFormatter
SoapFormatter

# JSON deserialization risks (CA2326-CA2330)
TypeNameHandling\s*=\s*(?!.*None)        # JSON.NET TypeNameHandling != None
SimpleTypeResolver

# DataSet/DataTable (CA2350-CA2362)
DataTable\.ReadXml|DataSet\.ReadXml
ReadXmlSchema

# === ASP.NET / CONFIGURATION ===

# Missing antiforgery (CA3147, CA5391)
\[Http(Post|Put|Delete|Patch)\]          # State-changing endpoints
\[IgnoreAntiforgeryToken\]

# Insecure cookies (CA5382, CA5396)
CookieOptions|CookieBuilder
\.Secure\s*=\s*false
\.HttpOnly\s*=\s*false
SameSiteMode\.None

# CORS misconfiguration
AllowAnyOrigin|WithOrigins\("\*"\)
AllowCredentials.*AllowAnyOrigin

# Information disclosure
\.UseDeveloperExceptionPage
StackTrace|\.ToString\(\).*Exception

# Token/request validation disabled (CA5363, CA5404, CA5405)
RequireExpirationTime\s*=\s*false
ValidateLifetime\s*=\s*false
ValidateAudience\s*=\s*false
ValidateIssuer\s*=\s*false

# XML processing (CA3061, CA3075-CA3077)
XmlDocument|XmlTextReader
DtdProcessing\.Parse
XslCompiledTransform.*\.Load
new\s+XmlReaderSettings.*ProhibitDtd\s*=\s*false
```

## Analysis Checklist

| # | Check | Severity | CA Rules |
|---|-------|----------|----------|
| 1 | SQL/command injection — user input in queries or commands | CRITICAL | CA3001, CA3006 |
| 2 | BinaryFormatter or insecure deserializers | CRITICAL | CA2300-CA2315 |
| 3 | Hardcoded credentials, connection strings, or API keys | CRITICAL | CA5390 |
| 4 | Disabled certificate validation (callback returns true) | CRITICAL | CA5359 |
| 5 | XSS — unencoded user input in HTML output | HIGH | CA3002 |
| 6 | Path traversal — user input in file paths without validation | HIGH | CA3003 |
| 7 | TypeNameHandling != None in JSON.NET | HIGH | CA2326-CA2330 |
| 8 | Weak cryptographic algorithms (MD5, SHA1, DES, 3DES, RC2) | HIGH | CA5350, CA5351 |
| 9 | System.Random for security-sensitive values | HIGH | CA5394 |
| 10 | Deprecated TLS/SSL protocols or hardcoded protocol versions | HIGH | CA5364, CA5386, CA5397 |
| 11 | Missing antiforgery tokens on state-changing endpoints | MEDIUM | CA3147, CA5391 |
| 12 | Insecure cookie settings | MEDIUM | CA5382, CA5396 |
| 13 | CORS allows any origin with credentials | MEDIUM | — |
| 14 | Developer exception page without environment guard | MEDIUM | — |
| 15 | RSA key < 2048 bits or PBKDF2 iterations < 100,000 | MEDIUM | CA5379, CA5385 |
| 16 | DTD processing enabled in XML parsing (XXE risk) | MEDIUM | CA3075-CA3077 |
| 17 | Token validation checks disabled | MEDIUM | CA5404 |
| 18 | LDAP/XPath/regex injection from user input | MEDIUM | CA3005, CA3008, CA3012 |
| 19 | Missing HTTPS redirection or HSTS | LOW | — |
| 20 | Information disclosure in error responses | LOW | CA3004 |

## Severity Definitions

- **CRITICAL**: RCE, auth bypass, or data breach exploitable now. Fix immediately.
- **HIGH**: Exploitable under realistic conditions.
- **MEDIUM**: Requires unusual conditions, insider access, or chained attacks.
- **LOW**: Defensive hardening. Unlikely to be exploited in current deployment context.

## Project Context

Check the project type before running all checklist items. Skip ASP.NET-specific checklist
items (#11-14, #19) unless target code is explicitly web-facing.
