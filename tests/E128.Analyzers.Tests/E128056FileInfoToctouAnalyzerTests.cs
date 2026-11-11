using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128056FileInfoToctouAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_Reports_WhenExistsCheckFollowedByFileRead()
    {
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class Loader
                           {
                               async Task<byte[]?> LoadAsync(FileInfo fileInfo)
                               {
                                   if (!fileInfo.Exists) return null;
                                   return await {|E128056:File.ReadAllBytesAsync(fileInfo.FullName)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_Reports_WhenStaticExistsCheckFollowedByReadOfTheSamePath()
    {
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
                           using System.IO;
                           class Loader
                           {
                               string Load(string path)
                               {
                                   if (!File.Exists(path)) return string.Empty;
                                   return {|E128056:File.ReadAllText(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_NoReport_WhenTheExistsCheckGuardsADifferentPath()
    {
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
                           using System.IO;
                           class Loader
                           {
                               string Load(string first, string second)
                               {
                                   if (!File.Exists(first)) return string.Empty;
                                   return File.ReadAllText(second);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_NoReport_WhenTheSamePropertyIsReadOnADifferentInstance()
    {
        // The guarded value keys on the (receiver, member) pair, so configA.Path and configB.Path
        // name different files even though both bind to the same property symbol.
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
                           using System.IO;
                           class Config
                           {
                               public string Path { get; set; } = string.Empty;
                           }
                           class Loader
                           {
                               string Load(Config configA, Config configB)
                               {
                                   if (File.Exists(configA.Path)) return string.Empty;
                                   return File.ReadAllText(configB.Path);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_NoReport_WhenReadIsGuardedWithTryCatch()
    {
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class Loader
                           {
                               async Task<byte[]?> LoadAsync(FileInfo fileInfo)
                               {
                                   if (!fileInfo.Exists) return null;
                                   try
                                   {
                                       return await File.ReadAllBytesAsync(fileInfo.FullName);
                                   }
                                   catch (IOException)
                                   {
                                       return null;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_NoReport_WhenNoExistsCheckPresent()
    {
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class Loader
                           {
                               async Task<byte[]?> LoadAsync(FileInfo fileInfo)
                               {
                                   try
                                   {
                                       return await File.ReadAllBytesAsync(fileInfo.FullName);
                                   }
                                   catch (IOException)
                                   {
                                       return null;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_NoReport_WhenEachCheckAndReadBuildsItsOwnPath()
    {
        // Both arguments bind to Path.Combine. A call names no storage location, so it cannot
        // stand for a guarded value.
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
                           using System.IO;
                           class Loader
                           {
                               string Load(string dir)
                               {
                                   if (!File.Exists(Path.Combine(dir, "a.txt"))) return string.Empty;
                                   return File.ReadAllText(Path.Combine(dir, "b.txt"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_NoReport_WhenTheReadUsesADifferentPropertyOfTheSameObject()
    {
        // InputPath and OutputPath are separate properties, so the check guards a different value.
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
                           using System.IO;
                           class Options { public string InputPath { get; set; } = ""; public string OutputPath { get; set; } = ""; }
                           class Loader
                           {
                               string Load(Options options)
                               {
                                   if (!File.Exists(options.InputPath)) return string.Empty;
                                   return File.ReadAllText(options.OutputPath);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_Reports_WhenTheReadUsesTheGuardedProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
                           using System.IO;
                           class Options { public string Path { get; set; } = ""; }
                           class Loader
                           {
                               string Load(Options options)
                               {
                                   if (!File.Exists(options.Path)) return string.Empty;
                                   return {|E128056:File.ReadAllText(options.Path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_NoReport_WhenTheSameNameBelongsToAnotherScope()
    {
        // The nested path parameter is a different symbol from the one the check guarded.
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
                           using System.IO;
                           class Loader
                           {
                               string Load(string path)
                               {
                                   if (!File.Exists(path)) return string.Empty;

                                   string Read(string path) => File.ReadAllText(path);
                                   return Read(path);
                               }
                           }
                           """);
    }
}
