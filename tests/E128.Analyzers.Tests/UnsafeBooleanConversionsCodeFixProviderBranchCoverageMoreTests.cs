using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeBooleanConversionsCodeFixProviderBranchCoverageMoreTests
{
    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<UnsafeBooleanConversionsAnalyzer, UnsafeBooleanConversionsCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCallUsesAStaticImportWithoutAMemberAccess()
    {
        return VerifyNoFixAsync("""
            using static System.Runtime.CompilerServices.Unsafe;
            class C
            {
                bool M(ref int value) => {|E128115:As<int, bool>(ref value)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheSourceTypeIsSpelledWithItsFrameworkName()
    {
        return VerifyNoFixAsync("""
            using System;
            using System.Runtime.CompilerServices;
            class C
            {
                bool M(ref int value) => {|E128115:Unsafe.As<Int32, bool>(ref value)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheBoolSourceHasAFrameworkNamedTarget()
    {
        return VerifyNoFixAsync("""
            using System;
            using System.Runtime.CompilerServices;
            class C
            {
                int M(ref bool value) => {|E128115:Unsafe.As<bool, Int32>(ref value)|};
            }
            """);
    }
}
