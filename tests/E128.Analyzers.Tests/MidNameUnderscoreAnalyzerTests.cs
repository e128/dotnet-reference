using System.Threading.Tasks;
using E128.Analyzers.Style;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MidNameUnderscoreAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_MidNameUnderscore_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static int {|E128063:Nots_supportedExtensions|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_MidNameUnderscore_TypeNameMangle_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static object {|E128063:Creates_enrichmentJsonOptions|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_SpectresPattern_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static string {|E128063:Spectres_terminal|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticMethod_MidNameUnderscore_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static void {|E128063:Process_batch|}() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticProperty_MidNameUnderscore_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static int {|E128063:Config_timeout|} => 42;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InternalStaticField_MidNameUnderscore_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               internal static int {|E128063:Batch_processor|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_LeadingUnderscore_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static int _count;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_HungarianSPrefix_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static int s_count;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_HungarianMPrefix_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static int m_count;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_HungarianTPrefix_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static int t_count;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_HungarianWithMidNameUnderscore_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static int {|E128063:s_batch_count|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_DoubleUnderscorePrefix_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static int __hidden;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstField_WithUnderscore_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private const int MAX_BATCH_SIZE = 100;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_NoUnderscore_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private static int count;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PublicStaticField_MidNameUnderscore_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               public static int Nots_supported;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ProtectedStaticField_MidNameUnderscore_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               protected static int Nots_supported;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InstanceField_MidNameUnderscore_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           class C
                           {
                               private int Nots_supported;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PartialProperty_ReportsOnce()
    {
        // A partial member arrives as two symbol events. The rule reports at the
        // defining part and skips the implementing part.
        return AnalyzerTestRunner.VerifyAsync<MidNameUnderscoreAnalyzer>("""
                           partial class C
                           {
                               private static partial int {|E128063:Some_name|} { get; }

                               private static partial int Some_name { get => 1; }
                           }
                           """);
    }
}
