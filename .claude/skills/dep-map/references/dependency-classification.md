# dep-map — Dependency Classification

Reference tables for classifying dependencies during Step 3 scanning.

---

## Package classification

| Package name / group ID pattern              | Classification          |
| -------------------------------------------- | ----------------------- |
| `Microsoft.*`, `System.*`, `Azure.*`         | Microsoft / Azure SDK   |
| `org.springframework.*`                      | Spring framework        |
| `org.flowable.*`, `com.flowable.*`           | Flowable workflow       |
| `org.apache.*`, `ch.qos.*`, `io.micrometer.*`| Apache / observability  |
| Same module prefix as the repo being scanned | First-party (same codebase) |
| All others                                   | Third-party open source |

**First-party detection:** If the package/module prefix matches patterns already seen in other repos within the scope scan root (same company namespace, same Go module prefix, same npm org), classify as first-party cross-repo integration.

---

## External service integration detection

A package is an **external service integration** if its name ends in:

- `.Api.Client`, `.Api.Client.Abstractions`, `.Private.Api.Client`, `.Public.Api.Client`
- `.Client` (if the base name is clearly a service, not a framework utility)
- `-api-client`, `-sdk-client` (Java/npm artifact IDs)
- `-client` (Go / npm, when the base name is a service)

If a client package has no known mapping, record it verbatim and mark:
`⚠ service mapping unknown — verify against repo list`.

---

## Test-only dependencies (omit from production tables)

| Pattern                                      | Skip reason          |
| -------------------------------------------- | -------------------- |
| `xunit.*`, `NUnit.*`, `MSTest.*`             | .NET test frameworks |
| `Moq`, `NSubstitute`, `FakeItEasy`           | Mocking              |
| `testcontainers.*`, `org.testcontainers.*`   | Containerized tests  |
| `Bogus`, `AutoFixture`                       | Test data            |
| `pytest`, `unittest`                         | Python test          |
| `jest`, `vitest`, `cypress`, `playwright`    | JS/TS test           |
| Any package in a `*.Tests.csproj` exclusively| Test project only    |
