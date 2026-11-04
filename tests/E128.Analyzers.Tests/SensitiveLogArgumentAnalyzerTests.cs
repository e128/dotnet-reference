using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SensitiveLogArgumentAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<SensitiveLogArgumentAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
                .AddPackages([new PackageIdentity("Microsoft.Extensions.Logging.Abstractions", "10.0.13")]),
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PasswordArgument_Fires()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, string password)
                               {
                                   logger.LogInformation("Login with {Password}", {|E128166:password|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MemberAccessToken_Fires()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class Session { public string AccessToken { get; set; } = ""; }
                           class C
                           {
                               void M(ILogger logger, Session session)
                               {
                                   logger.LogDebug("Session {Token}", {|E128166:session.AccessToken|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterpolatedHole_Fires()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, string apiKey)
                               {
                                   logger.LogWarning({|E128166:$"key {apiKey}"|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CancellationTokenAndMetadataNames_Silent()
    {
        return VerifyAsync("""
                           using System.Threading;
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, CancellationToken cancellationToken, int tokenCount, string secretName)
                               {
                                   logger.LogInformation("State {Token} {Count} {Name}", cancellationToken, tokenCount, secretName);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OrdinaryArguments_Silent()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           class C
                           {
                               void M(ILogger logger, string userId, int count)
                               {
                                   logger.LogInformation("User {UserId} sent {Count}", userId, count);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonLoggerCall_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               void Print(string text, string value) { }

                               void M(string password)
                               {
                                   Print("pwd", password);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TestCompilation_Silent()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.Logging;
                           namespace Xunit
                           {
                               public sealed class FactAttribute : System.Attribute
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
                           """);
    }
}
