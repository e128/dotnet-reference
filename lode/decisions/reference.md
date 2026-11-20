# Reference App Decisions
*Updated: 2026-11-20T17:16:51Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## CliApp
- CliApp's result union closes the case list, and each case maps to its own exit code. The null arm is the empty case the compiler requires.

## InMemoryGreetingRepository
- Relies on save order for recency because GreetingService stamps CreatedAt at save time. As a result, save order and timestamp order agree. It binds the list and its last index once because the JIT cannot hoist a field read across the indexer call.

## GreetingService
- Treats a null name as "use the default" and a present but blank name as an expected failure.

## Program
- Keeps per-request Information logs out of the request path because each costs a console write and a lock per request.

Related: [Decision Records](summary.md) explains how to use this page.
