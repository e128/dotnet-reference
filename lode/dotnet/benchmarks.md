# Benchmarks
*Updated: 2026-11-09T21:51:08Z*

## Overview

`benchmarks/E128.Benchmarks` is an executable project that holds BenchmarkDotNet starter benchmarks. A human must review the cases. They cover the public surface, not a measured hot path. The solution file lists the project in its `/benchmarks/` folder.

| Class                   | Subject                                                                    |
| ----------------------- | -------------------------------------------------------------------------- |
| `CoreBenchmarks`        | `Greeter`, `GreetingService`, and `InMemoryGreetingRepository`             |
| `WebEndpointBenchmarks` | `GET /`, `POST /greetings`, and `GET /greetings` of the Web project        |
| `AnalyzerBenchmarks`    | Three E128 analyzers, each alone and together, over one sample compilation |

Each class uses `[MemoryDiagnoser]`. The packages are in [dep-map.md](../dotnet-reference/dep-map.md).

## Invariants

- BenchmarkDotNet rejects a sealed benchmark class. Each class is public and unsealed. A doc comment on the class states this.
- E128005 skips a class whose base type is `object`. `SealedClassTests` loads only the Core assembly. Neither gate covers this project. See [architecture-testing.md](architecture-testing.md).
- `benchmarks/.globalconfig` sets CA1515 to `none` at `global_level = 101`. BenchmarkDotNet needs public types. See [analyzers.md](analyzers.md).
- `EntryPoint` replaces a top-level `Program`. The Web project exposes `Program` through `InternalsVisibleTo`, and a second `Program` causes CS0436.
- `EntryPoint.Main` sets the working directory to `AppContext.BaseDirectory`. `WebApplicationFactory` reads `MvcTestingAppManifest.json` from the working directory. A relative path in a command-line argument resolves against that folder, so pass absolute paths.
- `EntryPoint.Main` adds `InProcessEmitToolchain` to the default config, so the benchmarks run in the host process. JIT and tiering state carries over between cases.
- `WebEndpointBenchmarks` runs the app behind the test server. The numbers exclude the network and the Kestrel socket layer. A `FixedGreetingRepository` replaces the in-memory repository, so stored state stays constant and the numbers do not drift. The repository cost is not measured.
- `AnalyzerBenchmarks` builds the sample compilation once, so the numbers show warm analyzer cost. It throws when the sample has a compile error. `GlobalSetup` throws when an analyzer reports no diagnostic.
- `CoreBenchmarks` uses a `FixedGreetingRepository` with no greetings for the save benchmark, and a seeded in-memory repository for the read benchmark.

## Running

Run the project in Release. The `EntryPoint` doc comment holds the command line. Prefer a Linux container, because production runs on Linux. Treat a macOS run as a reference only. Build the project alone with `scripts/build.sh --project E128.Benchmarks`.

The setup step records no timings. A comparison against a base commit needs a Linux container run first. Until then the performance comparison stays deferred.

## Related

- [performance.md](performance.md): runtime knobs and the profiling ladder
- [project-structure.md](project-structure.md): solution layout
