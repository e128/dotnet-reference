using System.Threading.Tasks;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PrimaryConstructorBackingFieldE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ReadonlyFieldFromPrimaryCtorParam_Fires()
    {
        Assert.Fail("AC5a: readonly field assigned from primary ctor param fires E128017");
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MutableFieldFromPrimaryCtorParam_DoesNotFire()
    {
        Assert.Fail("AC5b: mutable (non-readonly) field must not fire — may be reassigned");
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StructWithPrimaryCtorField_DoesNotFire()
    {
        Assert.Fail("AC5c: structs do not capture primary ctor params — field is necessary");
        return Task.CompletedTask;
    }
}
