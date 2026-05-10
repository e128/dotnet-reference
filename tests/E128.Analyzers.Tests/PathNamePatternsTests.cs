using E128.Analyzers.FileSystem;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PathNamePatternsTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_ContainsPath_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("filePath"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_ContainsPathUpperCase_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("FilePath"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_ContainsPathAllCaps_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("FILEPATH"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_StandalonePathWord_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("path"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_PrefixedPath_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("inputPath"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_ContainsDir_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("outputDir"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_ContainsDirUpperCase_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("OutputDir"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_ContainsDirectory_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("baseDirectory"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_ContainsDirectoryUpperCase_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("BaseDirectory"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_StandaloneDirectory_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("directory"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_PlainName_ReturnsFalse()
    {
        Assert.False(PathNamePatterns.IsPathName("name"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_FileNameWord_ReturnsFalse()
    {
        Assert.False(PathNamePatterns.IsPathName("fileName"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_UnrelatedWord_ReturnsFalse()
    {
        Assert.False(PathNamePatterns.IsPathName("count"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_EmptyString_ReturnsFalse()
    {
        Assert.False(PathNamePatterns.IsPathName(string.Empty));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_XPathExclusion_ReturnsFalse()
    {
        Assert.False(PathNamePatterns.IsPathName("xpath"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_XPathExclusionUpperCase_ReturnsFalse()
    {
        Assert.False(PathNamePatterns.IsPathName("xpaths"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_XPathExclusionMixedCase_ReturnsFalse()
    {
        Assert.False(PathNamePatterns.IsPathName("XPath"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_XPathContainingPath_ExclusionWins()
    {
        // "xpathFilter" contains both "xpath" (exclusion) and "path" (positive pattern).
        // The exclusion is checked first and should suppress the match.
        Assert.False(PathNamePatterns.IsPathName("xpathFilter"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_Paths_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("paths"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_Directories_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("directories"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathName_Dirs_ReturnsTrue()
    {
        Assert.True(PathNamePatterns.IsPathName("dirs"));
    }
}
