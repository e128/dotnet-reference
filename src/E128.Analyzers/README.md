# E128.Analyzers

Roslyn analyzers enforcing E128 conventions. Install as a NuGet package to get compile-time warnings and errors.

## Rules

| Rule ID   | Category    | Description                                                    |
| --------- | ----------- | -------------------------------------------------------------- |
| E128001   | Design      | Use FileInfo or DirectoryInfo instead of string for file paths |
| E128002   | Style       | Use string.Empty instead of ""                                 |
| E128003   | Reliability | Use TimeProvider instead of DateTime.Now / DateTimeOffset.Now  |
| E128004   | Reliability | Use IHttpClientFactory instead of new HttpClient()             |
| E128005   | Design      | Seal classes that have no subclasses                           |

## Installation

```xml
<PackageReference Include="E128.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

## Configuration

All rules are enabled by default at Warning severity. Override in `.editorconfig` or `.globalconfig`:

```ini
dotnet_diagnostic.E128001.severity = error
dotnet_diagnostic.E128002.severity = none
```
