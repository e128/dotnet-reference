using System.Threading.Tasks;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PrimaryConstructorBackingFieldE128CodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task BackingField_Removed_ReferencesReplacedWithParameter()
    {
        Assert.Fail("AC5d: code fix removes field and replaces single reference with param name");
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BackingField_MultipleReferences_AllReplaced()
    {
        Assert.Fail("AC5e: code fix replaces all references across multiple methods");
        return Task.CompletedTask;
    }
}
