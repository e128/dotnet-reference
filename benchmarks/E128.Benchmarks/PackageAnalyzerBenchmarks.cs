using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Benchmarks;

/// <summary>
///     Starter benchmark for the consumer critical path of the E128.Analyzers package: every analyzer in the
///     package runs over one ordinary service-style compilation, as it does in a consumer build. The compilation
///     references the whole shared framework and is built once, so the number shows warm analyzer cost, not
///     first-compile cost. The code fixers are not measured here.
///     BenchmarkDotNet requires an unsealed class, so this class is not sealed.
/// </summary>
[MemoryDiagnoser]
public class PackageAnalyzerBenchmarks
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;
        using System.Net.Http;
        using System.Text;
        using System.Threading;
        using System.Threading.Tasks;

        namespace Sample;

        public sealed record Order(int Id, string Customer, decimal Total, DateTime CreatedAt);

        public interface IOrderStore
        {
            Task<IReadOnlyList<Order>> LoadAsync(CancellationToken cancellationToken);
        }

        public sealed class OrderReport
        {
            private readonly IOrderStore _store;
            private readonly HttpClient _client;

            public OrderReport(IOrderStore store, HttpClient client)
            {
                _store = store;
                _client = client;
            }

            public async Task<string> BuildAsync(string path, CancellationToken cancellationToken)
            {
                var orders = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
                var builder = new StringBuilder();
                foreach (var group in orders.GroupBy(order => order.Customer))
                {
                    var total = group.Sum(order => order.Total);
                    builder.Append(group.Key + ": " + total);
                    builder.AppendLine();
                }

                var status = await _client.GetStringAsync(new Uri("https://example.com/status"), cancellationToken)
                    .ConfigureAwait(false);
                if (status == "down" || status == "maintenance" || status == "degraded")
                {
                    return string.Empty;
                }

                var text = builder.ToString();
                await File.WriteAllTextAsync(path, text, cancellationToken).ConfigureAwait(false);
                return File.ReadAllText(path);
            }

            public static Order? Latest(List<Order> orders)
            {
                return orders.OrderByDescending(order => order.CreatedAt).FirstOrDefault();
            }

            public static DateTime ParseDate(string value)
            {
                return DateTime.Parse(value);
            }
        }
        """;

    private readonly CSharpCompilation _compilation = CreateCompilation();

    private readonly ImmutableArray<DiagnosticAnalyzer> _analyzers =
    [
        .. typeof(ExcessiveInheritanceAnalyzer).Assembly.GetTypes()
            .Where(static type => !type.IsAbstract
                && typeof(DiagnosticAnalyzer).IsAssignableFrom(type)
                && Attribute.IsDefined(type, typeof(DiagnosticAnalyzerAttribute), inherit: false))
            .Select(static type => (DiagnosticAnalyzer)(Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Cannot create analyzer {type.FullName}.")))
    ];

    [GlobalSetup]
    public async Task VerifyPackageReportsAsync()
    {
        // An empty analyzer set or a run that reports nothing times only the cheap path. Fail fast instead.
        if (_analyzers.IsEmpty)
        {
            throw new InvalidOperationException("No analyzer was found in the E128.Analyzers assembly.");
        }

        var diagnostics = await AllPackageAnalyzersAsync().ConfigureAwait(false);
        if (!diagnostics.IsEmpty)
        {
            return;
        }

        throw new InvalidOperationException("The package analyzers reported no diagnostic for the sample source.");
    }

    [Benchmark]
    public Task<ImmutableArray<Diagnostic>> AllPackageAnalyzersAsync()
    {
        return _compilation
            .WithAnalyzers(_analyzers)
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    private static CSharpCompilation CreateCompilation()
    {
        var platformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("The trusted platform assembly list is unavailable.");

        var references = platformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            "PackageBenchmarkSample",
            [CSharpSyntaxTree.ParseText(Source, cancellationToken: CancellationToken.None)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics(CancellationToken.None)
            .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error);

        return errors.Any()
            ? throw new InvalidOperationException("The sample source does not compile.")
            : compilation;
    }
}
