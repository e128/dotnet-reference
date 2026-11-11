using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LoggerArgumentGuardAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithLogging };

    private static readonly ReferenceAssemblies Net100WithLogging = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.Logging.Abstractions", "10.0.13")]);

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheMessageIsInterpolated()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int count)
                               {
                                   {|E128122:logger.LogInformation($"count {count}")|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheMessageIsConcatenated()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, string name)
                               {
                                   {|E128122:logger.LogWarning("name " + name)|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMessageIsAConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger)
                               {
                                   logger.LogInformation("started");
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAdditionIsNumeric()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int a, int b)
                               {
                                   logger.LogInformation("{Sum}", a + b);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnEarlyReturnGuardPrecedesTheCall()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenACompoundConditionGuardsTheCall()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardIsDisjoinedWithAnotherCondition()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardChecksAnotherLevel()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallIsInTheElseBranch()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAHelperPropertyHoldsTheGuard()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALocalHoldsTheGuard()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheHelperChecksAnotherLevel()
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>("""
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
                           """, options: Options);
    }
}
