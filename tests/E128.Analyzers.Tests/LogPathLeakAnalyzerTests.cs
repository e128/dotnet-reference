using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LogPathLeakAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheArgumentIsFileInfoFullName()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using System.IO;
                           using Microsoft.Extensions.Logging;

                           namespace Microsoft.Extensions.Logging
                           {
                               public interface ILogger { }
                               public static class LoggerExtensions
                               {
                                   public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                               }
                           }

                           class C
                           {
                               void Run(ILogger logger, FileInfo file)
                               {
                                   logger.LogInformation("Reading {File}", {|E128256:file.FullName|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheInterpolationHoldsFullName()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using System.IO;
                           using Microsoft.Extensions.Logging;

                           namespace Microsoft.Extensions.Logging
                           {
                               public interface ILogger { }
                               public static class LoggerExtensions
                               {
                                   public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                               }
                           }

                           class C
                           {
                               void Run(ILogger logger, FileInfo file)
                               {
                                   logger.LogInformation($"Reading {[|file.FullName|]}");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheArgumentIsGetFullPath()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using System.IO;
                           using Microsoft.Extensions.Logging;

                           namespace Microsoft.Extensions.Logging
                           {
                               public interface ILogger { }
                               public static class LoggerExtensions
                               {
                                   public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                               }
                           }

                           class C
                           {
                               void Run(ILogger logger)
                               {
                                   logger.LogInformation("Reading {File}", {|E128256:Path.GetFullPath("notes.txt")|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsFileName()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using System.IO;
                           using Microsoft.Extensions.Logging;

                           namespace Microsoft.Extensions.Logging
                           {
                               public interface ILogger { }
                               public static class LoggerExtensions
                               {
                                   public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                               }
                           }

                           class C
                           {
                               void Run(ILogger logger, FileInfo file)
                               {
                                   logger.LogInformation("Reading {File}", file.Name);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsAConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using Microsoft.Extensions.Logging;

                           namespace Microsoft.Extensions.Logging
                           {
                               public interface ILogger { }
                               public static class LoggerExtensions
                               {
                                   public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                               }
                           }

                           class C
                           {
                               void Run(ILogger logger)
                               {
                                   logger.LogInformation("Reading {File}", "notes.txt");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesFullNameWithName()
    {
        return CodeFixTestRunner.RunAsync<LogPathLeakAnalyzer, LogPathLeakCodeFixProvider>(
            """
            using System.IO;
            using Microsoft.Extensions.Logging;

            namespace Microsoft.Extensions.Logging
            {
                public interface ILogger { }
                public static class LoggerExtensions
                {
                    public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                }
            }

            class C
            {
                void Run(ILogger logger, FileInfo file)
                {
                    logger.LogInformation("Reading {File}", {|E128256:file.FullName|});
                }
            }
            """,
            """
            using System.IO;
            using Microsoft.Extensions.Logging;

            namespace Microsoft.Extensions.Logging
            {
                public interface ILogger { }
                public static class LoggerExtensions
                {
                    public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                }
            }

            class C
            {
                void Run(ILogger logger, FileInfo file)
                {
                    logger.LogInformation("Reading {File}", file.Name);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePathIsStagedInALocal()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using System.IO;
                           using Microsoft.Extensions.Logging;

                           namespace Microsoft.Extensions.Logging
                           {
                               public interface ILogger { }
                               public static class LoggerExtensions
                               {
                                   public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                               }
                           }

                           class C
                           {
                               void Run(ILogger logger, FileInfo file)
                               {
                                   var full = [|file.FullName|];
                                   logger.LogInformation("Reading {File}", full);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheArgumentIsFileSystemInfoToString()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using System.IO;
                           using Microsoft.Extensions.Logging;

                           namespace Microsoft.Extensions.Logging
                           {
                               public interface ILogger { }
                               public static class LoggerExtensions
                               {
                                   public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                               }
                           }

                           class C
                           {
                               void Run(ILogger logger, FileInfo file)
                               {
                                   logger.LogInformation("Reading {File}", [|file.ToString()|]);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLoggerMessageMethodIsCalled()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using System.IO;
                           using Microsoft.Extensions.Logging;

                           namespace Microsoft.Extensions.Logging
                           {
                               public interface ILogger { }
                               public sealed class LoggerMessageAttribute : System.Attribute { }
                           }

                           class C
                           {
                               [Microsoft.Extensions.Logging.LoggerMessage]
                               static void Reading(Microsoft.Extensions.Logging.ILogger logger, string filePath) { }

                               void Run(Microsoft.Extensions.Logging.ILogger logger, FileInfo file)
                               {
                                   Reading(logger, [|file.FullName|]);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStagedLocalIsTransformed()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using System.IO;
                           using Microsoft.Extensions.Logging;

                           namespace Microsoft.Extensions.Logging
                           {
                               public interface ILogger { }
                               public static class LoggerExtensions
                               {
                                   public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                               }
                           }

                           class C
                           {
                               void Run(ILogger logger, FileInfo file)
                               {
                                   var full = Path.GetFullPath(file.FullName);
                                   logger.LogInformation("Reading {File}", Path.GetFileName(full));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsNameOf()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using System.IO;
                           using Microsoft.Extensions.Logging;

                           namespace Microsoft.Extensions.Logging
                           {
                               public interface ILogger { }
                               public static class LoggerExtensions
                               {
                                   public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                               }
                           }

                           class C
                           {
                               void Run(ILogger logger, FileInfo file)
                               {
                                   logger.LogInformation("{Name}", nameof(file.FullName));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePathIsInAnExceptionMessage()
    {
        return AnalyzerTestRunner.VerifyAsync<LogPathLeakAnalyzer>("""
                           using System;
                           using System.IO;

                           class C
                           {
                               void Run(FileInfo file)
                               {
                                   throw new NotSupportedException($"No extractor for {file.FullName}");
                               }
                           }
                           """);
    }
}
