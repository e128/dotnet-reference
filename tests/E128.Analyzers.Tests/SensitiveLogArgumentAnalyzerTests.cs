using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SensitiveLogArgumentAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithLogging };

    private static readonly ReferenceAssemblies Net100WithLogging = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.Logging.Abstractions", "10.0.13")]);

    [Fact]
    [Trait("Category", "CI")]
    public Task PasswordArgument_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, string password)
                               {
                                   logger.LogInformation("Login with {Password}", {|E128166:password|});
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MemberAccessToken_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class Session { public string AccessToken { get; set; } = ""; }
                           class C
                           {
                               void M(ILogger logger, Session session)
                               {
                                   logger.LogDebug("Session {Token}", {|E128166:session.AccessToken|});
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterpolatedHole_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, string apiKey)
                               {
                                   logger.LogWarning({|E128166:$"key {apiKey}"|});
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CancellationTokenAndMetadataNames_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using System.Threading;
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, CancellationToken cancellationToken, int tokenCount, string secretName)
                               {
                                   logger.LogInformation("State {Token} {Count} {Name}", cancellationToken, tokenCount, secretName);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OrdinaryArguments_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, string userId, int count)
                               {
                                   logger.LogInformation("User {UserId} sent {Count}", userId, count);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonLoggerCall_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           class C
                           {
                               void Print(string text, string value) { }

                               void M(string password)
                               {
                                   Print("pwd", password);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoggerMessageMethod_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           static partial class Log
                           {
                               [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Login {Password}")]
                               static partial void LoggedIn(ILogger logger, string password);

                               public static void Run(ILogger logger, string password)
                               {
                                   LoggedIn(logger, {|E128166:password|});
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoggerMessageMethodWithSafeArgument_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           static partial class Log
                           {
                               [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Login {UserId}")]
                               static partial void LoggedIn(ILogger logger, string userId);

                               public static void Run(ILogger logger, string userId)
                               {
                                   LoggedIn(logger, userId);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConditionalAccess_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class User { public string? Password { get; set; } }
                           class C
                           {
                               void M(ILogger logger, User? user)
                               {
                                   logger.LogInformation("Login {Value}", {|E128166:user?.Password|});
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WrappedArguments_Fire()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class User { public string Password { get; set; } = ""; }
                           class C
                           {
                               void M(ILogger logger, User user, object password)
                               {
                                   logger.LogInformation("A {Value}", {|E128166:(string)password|});
                                   logger.LogInformation("B {Value}", {|E128166:(user.Password)|});
                                   logger.LogInformation("C {Value}", {|E128166:user.Password.Trim()|});
                                   logger.LogInformation("D {Value}", {|E128166:user.Password.ToString()|});
                                   logger.LogInformation("E {Value}", {|E128166:user?.Password?.Trim()|});
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExplicitParamsArray_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, string password)
                               {
                                   logger.LogInformation("Login {Password}", new object[] { {|E128166:password|} });
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EventIdExceptionAndTemplateArguments_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using System;
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, EventId tokenEvent, Exception secretException, string passwordMessage)
                               {
                                   logger.LogError(tokenEvent, secretException, "Failed");
                                   logger.LogInformation(passwordMessage);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortWordInsideLongerWord_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, int processNumber, int accessNumber)
                               {
                                   logger.LogInformation("Run {Process} {Access}", processNumber, accessNumber);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WholeWordShortNames_Fire()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, string ssn, string userSsn, string dbPwd)
                               {
                                   logger.LogInformation("A {Value} {Other} {Third}", {|E128166:ssn|}, {|E128166:userSsn|}, {|E128166:dbPwd|});
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TestCompilation_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>("""
                           using Microsoft.Extensions.Logging;
                           namespace Xunit
                           {
                               public sealed class FactAttribute : System.Attribute
                               {
                               }
                           }

                           class Tests
                           {
                               [Xunit.Fact]
                               public void RealTest()
                               {
                               }
                           }

                           class C
                           {
                               void M(ILogger logger, string password)
                               {
                                   logger.LogInformation("Login {Password}", password);
                               }
                           }
                           """, options: Options);
    }
}
