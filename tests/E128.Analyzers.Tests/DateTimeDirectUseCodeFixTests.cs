using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeDirectUseCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<DateTimeDirectUseAnalyzer, DateTimeDirectUseCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesDateTimeUtcNow_WithTimeProviderSystem()
    {
        const string source = """
            using System;
            class C
            {
                void M()
                {
                    var x = {|E128003:DateTime.UtcNow|};
                }
            }
            """;

        const string fixedCode = """
            using System;
            class C
            {
                void M()
                {
                    var x = TimeProvider.System.GetUtcNow().UtcDateTime;
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesDateTimeNow_WithTimeProviderSystemGetLocalNow()
    {
        const string source = """
            using System;
            class C
            {
                void M()
                {
                    var x = {|E128003:DateTime.Now|};
                }
            }
            """;

        const string fixedCode = """
            using System;
            class C
            {
                void M()
                {
                    var x = TimeProvider.System.GetLocalNow().DateTime;
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesDateTimeToday_WithTimeProviderSystemGetLocalNowDate()
    {
        const string source = """
            using System;
            class C
            {
                void M()
                {
                    var x = {|E128003:DateTime.Today|};
                }
            }
            """;

        const string fixedCode = """
            using System;
            class C
            {
                void M()
                {
                    var x = TimeProvider.System.GetLocalNow().Date;
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesDateTimeOffsetNow_WithTimeProviderSystemGetLocalNow()
    {
        const string source = """
            using System;
            class C
            {
                void M()
                {
                    DateTimeOffset x = {|E128003:DateTimeOffset.Now|};
                }
            }
            """;

        const string fixedCode = """
            using System;
            class C
            {
                void M()
                {
                    DateTimeOffset x = TimeProvider.System.GetLocalNow();
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesDateTimeOffsetUtcNow_WithTimeProviderSystemGetUtcNow()
    {
        const string source = """
            using System;
            class C
            {
                void M()
                {
                    DateTimeOffset x = {|E128003:DateTimeOffset.UtcNow|};
                }
            }
            """;

        const string fixedCode = """
            using System;
            class C
            {
                void M()
                {
                    DateTimeOffset x = TimeProvider.System.GetUtcNow();
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
