# Test Family Patterns
*Updated: 2026-07-13T17:48:48Z*

Standard test patterns for this codebase. Tests avoid mocking frameworks — collaborators
are either real lightweight implementations (e.g. `InMemoryGreetingRepository`) or small
hand-written test doubles (e.g. a fixed `TimeProvider`). Every test method carries
`[Trait("Category", "CI")]`.

## Pattern A: Stateless Classes (Extractors, Utilities, Algorithms)

```csharp
public sealed class {ClassName}Tests
{
    private readonly {ClassName} _sut = new();

    [Fact]
    [Trait("Category", "CI")]
    public void MethodName_WhenCondition_ExpectedBehavior()
    {
        // Direct method calls, assert return values
    }
}
```

## Pattern B: Service Classes with Dependencies

Real lightweight collaborators are preferred over fakes. Non-deterministic dependencies
(time, randomness) use a small nested test double.

```csharp
public sealed class {Service}Tests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly {Dependency} _{dependency} = new();
    private readonly {Service} _sut;

    public {Service}Tests()
    {
        _sut = new {Service}(_{dependency}, new FixedTimeProvider(FixedTime));
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task MethodName_WhenCondition_ExpectedBehavior()
    {
        // Call method, assert results + persisted side effects
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
```

## Pattern C: Repository Classes (In-Memory)

The reference repository (`InMemoryGreetingRepository`) holds state in memory with no
external I/O, so no `IAsyncLifetime` setup or connection teardown is needed.

```csharp
public sealed class {Repository}Tests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly {Repository} _sut = new();

    [Fact]
    [Trait("Category", "CI")]
    public async Task Save_ThenGetRecent_ReturnsSavedRecord()
    {
        // CRUD operations against the in-memory repository
    }
}
```

## Pattern D: Web Integration Tests

Uses `WebApplicationFactory<Program>` via `IClassFixture` (primary-constructor injection)
and drives the app over HTTP.

```csharp
public sealed class {Feature}Tests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Category", "CI")]
    public async Task Endpoint_ReturnsExpectedResponse()
    {
        // Drive the app over HTTP, assert status + body
    }
}
```

## Fake Service Conventions

- Hand-written fakes only (no NSubstitute, Moq, etc.)
- Create in test file if used once; separate `Fake{Interface}.cs` if shared
- Fakes implement only what the test needs

## Test Naming

```
MethodName_WhenCondition_ExpectedBehavior
```

## Critical Rules

- All test methods: `[Trait("Category", "CI")]`
- No "Arrange", "Act", "Assert" comments
- No `ConfigureAwait` in test code
- Inject a fixed `TimeProvider` (nested test double) for time-dependent code
- Test classes: `public sealed`
- File names match type names (MA0048)
- One public type per file
- `Assert.Equal`, `Assert.True`, `Assert.Contains` (no fluent)
- `StringComparison.Ordinal` for string assertions
- Async tests return `Task`
- `string.Empty` not `""`
