using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MidNameUnderscoreAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<MidNameUnderscoreAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateStaticField_MidNameUnderscore_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
                           class C
                           {
                               private int Nots_supported;
                           }
                           """);
    }
}
