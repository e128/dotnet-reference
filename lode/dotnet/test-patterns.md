# Test Family Patterns
*Updated: 2026-05-25T00:00:00Z*

Standard test patterns for this codebase. All tests use hand-written fakes (no mocking
frameworks), `NullLogger<T>.Instance` for loggers, and `[Trait("Category", "CI")]`.

## Pattern A: Stateless Classes (Extractors, Utilities, Algorithms)

```csharp
public sealed class {ClassName}Tests
{
    private readonly {ClassName} _sut = new(NullLogger<{ClassName}>.Instance);

    [Fact]
    [Trait("Category", "CI")]
    public void MethodName_WhenCondition_ExpectedBehavior()
    {
        // Direct method calls, assert return values
    }
}
```

## Pattern B: Service Classes with Dependencies (Faked)

```csharp
public sealed class {Service}Tests
{
    private readonly {Service} _sut;
    private readonly Fake{Dependency} _{dependency} = new();

    public {Service}Tests()
    {
        _sut = new {Service}(_{dependency}, NullLogger<{Service}>.Instance);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task MethodName_WhenCondition_ExpectedBehavior()
    {
        // Setup fakes, call method, assert results + side effects
    }
}
```

## Pattern C: Repository/Database Classes (SQLite In-Memory)

```csharp
public sealed class {Repository}Tests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private {Repository} _sut = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var initializer = new DatabaseInitializer(NullLogger<DatabaseInitializer>.Instance);
        await initializer.InitializeAsync(_connection);
        _sut = new {Repository}(NullLogger<{Repository}>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Insert_ThenGet_ReturnsInsertedRecord()
    {
        // CRUD operations against in-memory SQLite
    }
}
```

## Pattern D: Integration Tests

```csharp
public sealed class {Feature}IntegrationTests
{
    [Fact]
    [Trait("Category", "CI")]
    public async Task EndToEnd_WhenInput_ProducesExpectedOutput()
    {
        // Full pipeline test with faked dependencies
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
- `NullLogger<T>.Instance` for logger deps
- `Options.Create(new {Options}())` for IOptions deps
- Test classes: `public sealed`
- File names match type names (MA0048)
- One public type per file
- `Assert.Equal`, `Assert.True`, `Assert.Contains` (no fluent)
- `StringComparison.Ordinal` for string assertions
- Async tests return `Task`
- `string.Empty` not `""`
