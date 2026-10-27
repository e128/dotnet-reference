using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LoggerArgumentGuardCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<
            LoggerArgumentGuardAnalyzer,
            LoggerArgumentGuardCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
                .AddPackages([new PackageIdentity("Microsoft.Extensions.Logging.Abstractions", "10.0.13")]),
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_WrapsTheCallInAnIsEnabledGuard()
    {
        return VerifyFixAsync(
            """
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count)
                {
                    {|E128122:logger.LogInformation($"count {count}")|};
                }
            }
            """,
            """
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count)
                {
                    if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
                    {
                        logger.LogInformation($"count {count}");
                    }
                }
            }
            """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCallIsAnUnbracedIfBody()
    {
        const string Code = """
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count, bool ok)
                {
                    if (ok)
                        {|E128122:logger.LogInformation($"count {count}")|};
                    else
                        Recover();
                }

                void Recover()
                {
                }
            }
            """;

        return VerifyFixAsync(Code, Code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheReceiverIsACall()
    {
        const string Code = """
            using Microsoft.Extensions.Logging;
            class C
            {
                ILogger Get() => null;

                void M(int count)
                {
                    {|E128122:Get().LogInformation($"count {count}")|};
                }
            }
            """;

        return VerifyFixAsync(Code, Code);
    }
}
