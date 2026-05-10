using E128.Analyzers.FileSystem;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class IoMethodCatalogTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_FileReadAllText_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "ReadAllText", out var info);

        Assert.True(found);
        Assert.Equal(0, info.ArgIndex);
        Assert.Equal(SuggestedType.FileInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_FileReadAllTextAsync_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "ReadAllTextAsync", out var info);

        Assert.True(found);
        Assert.Equal(0, info.ArgIndex);
        Assert.Equal(SuggestedType.FileInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_FileReadAllBytes_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "ReadAllBytes", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_FileWriteAllText_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "WriteAllText", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_FileCreate_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "Create", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_FileOpen_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "Open", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_FileExists_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "Exists", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_FileDelete_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "Delete", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_FileCopy_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "Copy", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_FileMove_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "Move", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_DirectoryGetFiles_ReturnsDirectoryInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("Directory", "GetFiles", out var info);

        Assert.True(found);
        Assert.Equal(0, info.ArgIndex);
        Assert.Equal(SuggestedType.DirectoryInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_DirectoryGetDirectories_ReturnsDirectoryInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("Directory", "GetDirectories", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.DirectoryInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_DirectoryEnumerateFiles_ReturnsDirectoryInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("Directory", "EnumerateFiles", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.DirectoryInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_DirectoryEnumerateDirectories_ReturnsDirectoryInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("Directory", "EnumerateDirectories", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.DirectoryInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_DirectoryCreateDirectory_ReturnsDirectoryInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("Directory", "CreateDirectory", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.DirectoryInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_DirectoryDelete_ReturnsDirectoryInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("Directory", "Delete", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.DirectoryInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_DirectoryExists_ReturnsDirectoryInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("Directory", "Exists", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.DirectoryInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_DirectoryMove_ReturnsDirectoryInfo()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("Directory", "Move", out var info);

        Assert.True(found);
        Assert.Equal(SuggestedType.DirectoryInfo, info.Suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_UnknownClass_ReturnsFalse()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("Path", "Combine", out _);

        Assert.False(found);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_KnownClassUnknownMethod_ReturnsFalse()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "NonExistentMethod", out _);

        Assert.False(found);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_ClassNameIsCaseSensitive()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("file", "ReadAllText", out _);

        Assert.False(found);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetMethodInfo_MethodNameIsCaseSensitive()
    {
        var found = IoMethodCatalog.TryGetMethodInfo("File", "readalltext", out _);

        Assert.False(found);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathMethod_Combine_ReturnsTrue()
    {
        Assert.True(IoMethodCatalog.IsPathMethod("Combine"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathMethod_GetDirectoryName_ReturnsTrue()
    {
        Assert.True(IoMethodCatalog.IsPathMethod("GetDirectoryName"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathMethod_GetFileName_ReturnsTrue()
    {
        Assert.True(IoMethodCatalog.IsPathMethod("GetFileName"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathMethod_GetFullPath_ReturnsTrue()
    {
        Assert.True(IoMethodCatalog.IsPathMethod("GetFullPath"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathMethod_UnknownMethod_ReturnsFalse()
    {
        Assert.False(IoMethodCatalog.IsPathMethod("GetExtension"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsPathMethod_IsCaseSensitive()
    {
        Assert.False(IoMethodCatalog.IsPathMethod("combine"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetConstructorInfo_FileInfo_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetConstructorInfo("FileInfo", out var suggestion);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetConstructorInfo_DirectoryInfo_ReturnsDirectoryInfo()
    {
        var found = IoMethodCatalog.TryGetConstructorInfo("DirectoryInfo", out var suggestion);

        Assert.True(found);
        Assert.Equal(SuggestedType.DirectoryInfo, suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetConstructorInfo_StreamReader_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetConstructorInfo("StreamReader", out var suggestion);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetConstructorInfo_StreamWriter_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetConstructorInfo("StreamWriter", out var suggestion);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetConstructorInfo_FileStream_ReturnsFileInfo()
    {
        var found = IoMethodCatalog.TryGetConstructorInfo("FileStream", out var suggestion);

        Assert.True(found);
        Assert.Equal(SuggestedType.FileInfo, suggestion);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetConstructorInfo_UnknownType_ReturnsFalse()
    {
        var found = IoMethodCatalog.TryGetConstructorInfo("BinaryReader", out _);

        Assert.False(found);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetConstructorInfo_IsCaseSensitive()
    {
        var found = IoMethodCatalog.TryGetConstructorInfo("fileinfo", out _);

        Assert.False(found);
    }
}
