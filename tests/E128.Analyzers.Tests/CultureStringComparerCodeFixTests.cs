using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CultureStringComparerCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<
            CultureStringComparerAnalyzer,
            CultureStringComparerCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheCurrentCultureComparer()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                object M()
                {
                    return new Dictionary<string, int>({|E128132:StringComparer.CurrentCulture|});
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                object M()
                {
                    return new Dictionary<string, int>(StringComparer.Ordinal);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheQualifiedReceiver()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                object M()
                {
                    return new HashSet<string>({|E128132:System.StringComparer.InvariantCultureIgnoreCase|});
                }
            }
            """,
            """
            using System.Collections.Generic;
            class C
            {
                object M()
                {
                    return new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesAUsingStaticIdentifier()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            using static System.StringComparer;
            class C
            {
                object M()
                {
                    return new Dictionary<string, int>({|E128132:CurrentCultureIgnoreCase|});
                }
            }
            """,
            """
            using System.Collections.Generic;
            using static System.StringComparer;
            class C
            {
                object M()
                {
                    return new Dictionary<string, int>(OrdinalIgnoreCase);
                }
            }
            """);
    }
}
