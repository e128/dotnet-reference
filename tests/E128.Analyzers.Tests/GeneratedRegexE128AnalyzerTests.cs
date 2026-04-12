using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<GeneratedRegexAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private const string PartialImpl = "    private static partial System.Text.RegularExpressions.Regex DigitsOnly() => null!;";

    #region E128011 — Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_NoArgs_OnlyPattern_Fires()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128011:GeneratedRegex(@"\d+")|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_WithOptions_NoTimeout_Fires()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128011:GeneratedRegex(@"\d+", RegexOptions.None)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    #endregion E128011 — Fires

    #region E128011 — Does not fire

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_WithPositionalTimeout_NoFire()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [GeneratedRegex(@"\d+", RegexOptions.None, 1000)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_WithTimeoutAndCulture_NoFire()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [GeneratedRegex(@"\d+", RegexOptions.None, 1000, "en-US")]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    #endregion E128011 — Does not fire

    #region E128012 — Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_CompiledAlone_FiresE128012()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128011:{|E128012:GeneratedRegex(@"\d+", RegexOptions.Compiled)|}|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_CompiledOrIgnoreCase_FiresE128012()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128011:{|E128012:GeneratedRegex(@"\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase)|}|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_CompiledNamedArgument_FiresE128012()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128011:{|E128012:GeneratedRegex(@"\d+", options: RegexOptions.Compiled)|}|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_CompiledWithTimeout_FiresE128012()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128012:GeneratedRegex(@"\d+", RegexOptions.Compiled, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    #endregion E128012 — Fires

    #region E128012 — Does not fire

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_RegexOptionsNone_NoE128012()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128011:GeneratedRegex(@"\d+", RegexOptions.None)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_IgnoreCaseOnly_NoE128012()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128011:GeneratedRegex(@"\d+", RegexOptions.IgnoreCase)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_PatternOnly_NoE128012()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128011:GeneratedRegex(@"\d+")|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonGeneratedRegex_Compiled_NoE128012()
    {
        return VerifyAsync("""
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            public class GeneratedRegexAttribute : Attribute
            {
                public GeneratedRegexAttribute(string pattern, int options) { }
            }

            partial class C
            {
                [GeneratedRegex(@"\d+", 9)]
                private static void DigitsOnly() { }
            }
            """);
    }

    #endregion E128012 — Does not fire

    #region E128013 — Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_BackslashSStarFollowedByDotStar_FiresE128013()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128013:GeneratedRegex(@"\s*.*", RegexOptions.None, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_BackslashSStarFollowedByDotPlus_FiresE128013()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128013:GeneratedRegex(@"\s*.+", RegexOptions.None, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_AnchoredBackslashSStarDotPlusStar_FiresE128013()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128013:GeneratedRegex(@"^\s*(.+)\s*$", RegexOptions.None, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_LazyDotPlusBackslashSStarDollar_FiresE128013()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128013:GeneratedRegex(@"(.+?)\s*$", RegexOptions.None, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    #endregion E128013 — Fires

    #region E128013 — Does not fire

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_BackslashSStarFollowedByLiteral_NoE128013()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [GeneratedRegex(@"\s*=", RegexOptions.None, 1000)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_AnchoredBackslashSStarHashGroup_NoE128013()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [GeneratedRegex(@"^\s*#{1,6}\s+", RegexOptions.None, 1000)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_BackslashSStarBetweenLiterals_NoE128013()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [GeneratedRegex(@"foo\s*bar", RegexOptions.None, 1000)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_BackslashSStarFollowedByWordPlus_NoE128013()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [GeneratedRegex(@"\s*\w+", RegexOptions.None, 1000)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    #endregion E128013 — Does not fire

    #region E128014 — Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_DotPlusInsideQuantifiedGroup_FiresE128014()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128014:GeneratedRegex(@"(.+)+", RegexOptions.None, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_WordPlusInsideQuantifiedGroup_FiresE128014()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128014:GeneratedRegex(@"(\w+)+", RegexOptions.None, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_AStarInsideStarGroup_FiresE128014()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128014:GeneratedRegex(@"(a*)*", RegexOptions.None, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    #endregion E128014 — Fires

    #region E128014 — Does not fire

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_WordPlusNoOuterQuantifier_NoE128014()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [GeneratedRegex(@"(\w+)", RegexOptions.None, 1000)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GeneratedRegex_DotPlusNoGroup_NoE128014()
    {
        return VerifyAsync($$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [GeneratedRegex(@".+", RegexOptions.None, 1000)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    #endregion E128014 — Does not fire
}
