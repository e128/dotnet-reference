using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SensitiveLogArgumentBranchCoverageTests
{
    private static readonly ReferenceAssemblies Net100WithLogging = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.Logging.Abstractions", "10.0.13")]);

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>(
            code,
            options: new AnalyzerTestOptions { References = Net100WithLogging });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ElementsOfTheParamsArrayAtTheArgumentOrTheElement()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, string password, string user, string secret)
                {
                    logger.LogInformation("a {A}", {|E128166:(object)password|});
                    logger.LogInformation("b {A}", {|E128166:(password)|});
                    logger.LogInformation("c {A}", new object[] { {|E128166:password|}, user });
                    logger.LogInformation("d {A} {B}", user, {|E128166:secret|});
                    logger.LogInformation("e {A}", new object[] { {|E128166:(secret)|} });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NamesThroughCallsAccessesAndCasts()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class Session
            {
                public string? Token { get; set; }

                public string GetPassword() => "";
            }

            class C
            {
                void M(ILogger<C> logger, string? password, Session? session)
                {
                    logger.LogInformation("a {A}", {|E128166:password.ToString()|});
                    logger.LogInformation("b {A}", {|E128166:password?.ToString()|});
                    logger.LogInformation("c {A}", {|E128166:password!.Trim()|});
                    logger.LogInformation("d {A}", {|E128166:session?.Token|});
                    logger.LogInformation("e {A}", {|E128166:session?.Token?.Trim()|});
                    logger.LogInformation("f {A}", {|E128166:password as string|});
                    logger.LogInformation("g {A}", {|E128166:(string?)password|});
                    logger.LogInformation("h {A}", {|E128166:password|}!);
                    logger.LogInformation("i {A}", session!.GetPassword());
                    logger.LogInformation("j {A}", session.GetPassword().Trim('x'));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNameShowsMetadataOrACancellationToken()
    {
        return VerifyAsync("""
            using System.Threading;
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, string passwordName, string tokenType, int passwordCount, int secretLength, string tokenExpiry, string secretHeader, string tokenId, string passwordHint, CancellationToken cancellationToken, string processNumber, string cancellation_secret)
                {
                    logger.LogInformation("a {A} {B} {C} {D}", passwordName, tokenType, passwordCount, secretLength);
                    logger.LogInformation("b {A} {B} {C} {D}", tokenExpiry, secretHeader, tokenId, passwordHint);
                    logger.LogInformation("c {A} {B} {C}", cancellationToken, processNumber, cancellation_secret);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ShortSensitiveWordsOnlyAsWholeWords()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, string pwd, string user_ssn, string userSsn, string processNumber, string classNumber, string ssnumber)
                {
                    logger.LogInformation("a {A}", {|E128166:pwd|});
                    logger.LogInformation("b {A}", {|E128166:user_ssn|});
                    logger.LogInformation("c {A}", {|E128166:userSsn|});
                    logger.LogInformation("d {A}", processNumber);
                    logger.LogInformation("e {A}", classNumber);
                    logger.LogInformation("f {A}", ssnumber);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Handles_TheGenericLogCallAndTemplateStrings()
    {
        return VerifyAsync("""
            using System;
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, string password, string message, Exception exception)
                {
                    logger.LogInformation(password);
                    logger.LogError(exception, "failed {A}", {|E128166:password|});
                    logger.LogInformation(message);
                    logger.LogInformation("plain");
                    logger.LogInformation({|E128166:$"value {password}"|});
                    logger.LogInformation($"value {message}");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_GeneratedLoggerMessageMethodsThroughEitherPartialPart()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            partial class C
            {
                [LoggerMessage(1, LogLevel.Information, "first")]
                static partial void LogFirst(ILogger logger, string password);

                static partial void LogFirst(ILogger logger, string password) { }

                static partial void LogSecond(ILogger logger, string password);

                [LoggerMessage(2, LogLevel.Information, "second")]
                static partial void LogSecond(ILogger logger, string password) { }

                [LoggerMessage(3, LogLevel.Information, "third")]
                static partial void LogThird(ILogger logger, string user, int count = 0);

                static partial void LogThird(ILogger logger, string user, int count) { }

                void M(ILogger logger, string password, string user)
                {
                    LogFirst(logger, {|E128166:password|});
                    LogSecond(logger, {|E128166:password|});
                    LogThird(logger, user);
                    LogThird(logger, {|E128166:password|});
                }
            }
            """);
    }
}
