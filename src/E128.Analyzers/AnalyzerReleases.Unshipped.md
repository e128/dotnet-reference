### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E128142 | Security | Error    | Do not instantiate a Managed or CNG cryptography type
E128143 | Security | Error    | Use an RSA key of at least 3072 bits or an approved curve
E128144 | Security | Error    | Use an authenticated cipher mode
E128145 | Security | Error    | Use a strong PBKDF2 configuration
E128146 | Security | Error    | Use TLS 1.2 or later
E128147 | Security | Error    | RNGCryptoServiceProvider is deprecated
E128148 | Security | Error    | Do not use a cipher excluded by policy
E128149 | Security | Error    | Use the full 16-byte AES-GCM tag
E128150 | Security | Error    | Do not use SHA-3, SHAKE or KMAC
E128151 | Security | Error    | Use an HMAC key at least as long as the hash output

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128071 | Security     | Error        | Security     | Warning      | FIPS cryptography policy. A broken algorithm is a build-breaking defect.
E128075 | Security     | Error        | Security     | Warning      | FIPS cryptography policy. A predictable RNG in a security context is a build-breaking defect.
