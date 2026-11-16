using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ChainedStringReplaceCodeFixProviderBranchCoverageGapTests
{
    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<ChainedStringReplaceAnalyzer, ChainedStringReplaceCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheConditionReceiverIsAMemberAccess()
    {
        return VerifyNoFixAsync("""
            class C
            {
                private string _value = string.Empty;

                void M()
                {
                    {|E128098:while (this._value.Contains("--"))
                    {
                        _value = _value.Replace("--", "-");
                    }|}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheReplaceReceiverIsAMemberAccess()
    {
        return VerifyNoFixAsync("""
            class C
            {
                private string _value = string.Empty;

                void M()
                {
                    {|E128098:while (_value.Contains("--"))
                    {
                        _value = this._value.Replace("--", "-");
                    }|}
                }
            }
            """);
    }
}
