# Security

## Configuration Boundaries

Expose configurable values through strongly typed options bound to configuration.
Use safe defaults. Do not hardcode these values in source:

- URLs, cryptographic keys, secrets, or connection strings
- Allowed origins and CORS allowlists
- Authentication or authorization policy values
- File paths outside the application root

## Pooled Buffers

Clear every `ArrayPool<T>` and `MemoryPool<T>` buffer with `CryptographicOperations.ZeroMemory` before returning it.
Zero the whole rented array, not only the used bytes.
Zero it in a `finally` block so exceptions and early returns clear it.
Do not rely on `Return(buffer, clearArray: true)` for cryptographic zeroing.
Pass an exact-length slice to an API that requires one. A rented array can be oversized.

E128110 checks `ArrayPool<T>.Return`. Apply the `MemoryPool<T>` rule by hand.
The E128110 section of `src/E128.Analyzers/README.md` contains the reference pattern.
The compliant test is `Silent_WhenFinallyZeroesWholeArrayBeforeReturn` in `tests/E128.Analyzers.Tests/PooledBufferZeroingAnalyzerTests.cs`.

## Repository Privacy

Never include an email address in a commit message or PR body.
Do not include a real email address or the full name of the maintainer in a committed file.
Use role descriptions and reserved example addresses in documentation and test data.
Keep secrets and absolute home paths out of committed content.

## Related

- [Quality Gates](quality-gates.md): diagnostic safety and verification.
- [C# Conventions](../../lode/dotnet/csharp-conventions.md): time, HTTP, and async contracts.
