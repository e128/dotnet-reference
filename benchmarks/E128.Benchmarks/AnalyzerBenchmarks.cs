using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using E128.Analyzers.Design;
using E128.Analyzers.Performance;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Benchmarks;

/// <summary>
///     Starter benchmarks for the shipped analyzers. Each benchmark runs one analyzer, or all three,
///     over a small compilation that triggers every rule. The compilation is built once, so its semantic
///     caches stay warm. The numbers show warm analyzer cost, not first-compile cost.
///     BenchmarkDotNet requires an unsealed class, so this class is not sealed.
/// </summary>
[MemoryDiagnoser]
public class AnalyzerBenchmarks
{
    private const string Source = """
        using System;
        using System.Security.Cryptography;

        public class Level0 { }
        public class Level1 : Level0 { }
        public class Level2 : Level1 { }
        public class Level3 : Level2 { }

        public class Sample
        {
            public bool IsKnown(string text) => text == "a" || text == "b" || text == "c";

            public int Token()
            {
                var bytes = RandomNumberGenerator.GetBytes(1);
                return new Random().Next() + bytes.Length;
            }
        }
        """;

    private readonly CSharpCompilation _compilation = CreateCompilation();
    private readonly ImmutableArray<DiagnosticAnalyzer> _multiString = [new MultiStringEqualsOrChainAnalyzer()];
    private readonly ImmutableArray<DiagnosticAnalyzer> _inheritance = [new ExcessiveInheritanceAnalyzer()];
    private readonly ImmutableArray<DiagnosticAnalyzer> _insecureRandom = [new InsecureRandomInCryptoContextAnalyzer()];
    private readonly ImmutableArray<DiagnosticAnalyzer> _all;

    public AnalyzerBenchmarks()
    {
        _all = [.. _multiString, .. _inheritance, .. _insecureRandom];
    }

    [GlobalSetup]
    public async Task VerifyAnalyzersReportAsync()
    {
        // A benchmark of an analyzer that reports nothing times only the cheap path. Fail fast instead.
        foreach (var analyzers in new[] { _multiString, _inheritance, _insecureRandom })
        {
            var diagnostics = await RunAsync(analyzers).ConfigureAwait(false);
            if (diagnostics.IsEmpty)
            {
                throw new InvalidOperationException("An analyzer reported no diagnostic for the sample source.");
            }
        }
    }

    [Benchmark]
    public Task<ImmutableArray<Diagnostic>> MultiStringEqualsOrChainAsync() =>
        RunAsync(_multiString);

    [Benchmark]
    public Task<ImmutableArray<Diagnostic>> ExcessiveInheritanceAsync() =>
        RunAsync(_inheritance);

    [Benchmark]
    public Task<ImmutableArray<Diagnostic>> InsecureRandomInCryptoContextAsync() =>
        RunAsync(_insecureRandom);

    [Benchmark]
    public Task<ImmutableArray<Diagnostic>> AllThreeAsync() =>
        RunAsync(_all);

    private static CSharpCompilation CreateCompilation()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)
            ?? throw new InvalidOperationException("The runtime directory is unknown.");

        var references = new[]
        {
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Private.CoreLib.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Security.Cryptography.dll"))
        };

        var compilation = CSharpCompilation.Create(
            "BenchmarkSample",
            [CSharpSyntaxTree.ParseText(Source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        return errors.Any()
            ? throw new InvalidOperationException("The sample source does not compile.")
            : compilation;
    }

    private Task<ImmutableArray<Diagnostic>> RunAsync(ImmutableArray<DiagnosticAnalyzer> analyzers) =>
        _compilation
            .WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
}
