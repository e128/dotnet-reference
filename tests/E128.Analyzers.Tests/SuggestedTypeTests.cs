using E128.Analyzers.FileSystem;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SuggestedTypeTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void FileInfo_HasExpectedIntegerValue()
    {
        Assert.Equal(0, (int)SuggestedType.FileInfo);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void DirectoryInfo_HasExpectedIntegerValue()
    {
        Assert.Equal(1, (int)SuggestedType.DirectoryInfo);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void FileInfo_AndDirectoryInfo_AreDistinct()
    {
        Assert.NotEqual(SuggestedType.FileInfo, SuggestedType.DirectoryInfo);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void FileInfo_RoundTripsFromInt()
    {
        const SuggestedType value = SuggestedType.FileInfo;

        Assert.Equal(SuggestedType.FileInfo, value);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void DirectoryInfo_RoundTripsFromInt()
    {
        const SuggestedType value = SuggestedType.DirectoryInfo;

        Assert.Equal(SuggestedType.DirectoryInfo, value);
    }
}
