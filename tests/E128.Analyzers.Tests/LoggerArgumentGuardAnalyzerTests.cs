using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LoggerArgumentGuardAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<LoggerArgumentGuardAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
                .AddPackages([new PackageIdentity("Microsoft.Extensions.Logging.Abstractions", "10.0.13")]),
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheMessageIsInterpolated()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int count)
                               {
                                   {|E128122:logger.LogInformation($"count {count}")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheMessageIsConcatenated()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, string name)
                               {
                                   {|E128122:logger.LogWarning("name " + name)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMessageIsAConstant()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger)
                               {
                                   logger.LogInformation("started");
                               }
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAdditionIsNumeric()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int a, int b)
                               {
                                   logger.LogInformation("{Sum}", a + b);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnEarlyReturnGuardPrecedesTheCall()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int count)
                               {
                                   if (!logger.IsEnabled(LogLevel.Information))
                                   {
                                       return;
                                   }

                                   logger.LogInformation($"count {count}");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenACompoundConditionGuardsTheCall()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int count, bool verbose)
                               {
                                   if (verbose && logger.IsEnabled(LogLevel.Information))
                                   {
                                       logger.LogInformation($"count {count}");
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardIsDisjoinedWithAnotherCondition()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int count, bool verbose)
                               {
                                   if (logger.IsEnabled(LogLevel.Information) || verbose)
                                   {
                                       {|E128122:logger.LogInformation($"count {count}")|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardChecksAnotherLevel()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int count)
                               {
                                   if (logger.IsEnabled(LogLevel.Error))
                                   {
                                       {|E128122:logger.LogDebug($"count {count}")|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallIsInTheElseBranch()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int count)
                               {
                                   if (logger.IsEnabled(LogLevel.Information))
                                   {
                                   }
                                   else
                                   {
                                       {|E128122:logger.LogInformation($"count {count}")|};
                                   }
                               }
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAHelperPropertyHoldsTheGuard()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               private readonly ILogger _logger;

                               C(ILogger logger)
                               {
                                   _logger = logger;
                               }

                               private bool InfoEnabled => _logger.IsEnabled(LogLevel.Information);

                               void M(int count)
                               {
                                   if (InfoEnabled)
                                   {
                                       _logger.LogInformation($"count {count}");
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALocalHoldsTheGuard()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int count)
                               {
                                   var enabled = logger.IsEnabled(LogLevel.Information);
                                   if (enabled)
                                   {
                                       logger.LogInformation($"count {count}");
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheHelperChecksAnotherLevel()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               private readonly ILogger _logger;

                               C(ILogger logger)
                               {
                                   _logger = logger;
                               }

                               private bool ErrorEnabled => _logger.IsEnabled(LogLevel.Error);

                               void M(int count)
                               {
                                   if (ErrorEnabled)
                                   {
                                       {|E128122:_logger.LogInformation($"count {count}")|};
                                   }
                               }
                           }
                           """);
    }
}
