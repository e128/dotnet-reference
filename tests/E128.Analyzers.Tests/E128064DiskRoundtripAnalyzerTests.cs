using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128064DiskRoundtripAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<DiskRoundtripAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierA_WriteAllText_ReadAllText_Sync_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(string path, string content)
                               {
                                   File.WriteAllText(path, content);
                                   return {|E128064:File.ReadAllText(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierA_WriteAllText_ReadAllText_Async_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task<string> M(string path, string content)
                               {
                                   await File.WriteAllTextAsync(path, content);
                                   return {|E128064:await File.ReadAllTextAsync(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierA_WriteAllBytes_ReadAllBytes_Sync_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               byte[] M(string path, byte[] bytes)
                               {
                                   File.WriteAllBytes(path, bytes);
                                   return {|E128064:File.ReadAllBytes(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierA_WriteAllBytes_ReadAllBytes_Async_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task<byte[]> M(string path, byte[] bytes)
                               {
                                   await File.WriteAllBytesAsync(path, bytes);
                                   return {|E128064:await File.ReadAllBytesAsync(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierA_WriteAllLines_ReadAllLines_Sync_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string[] M(string path, string[] lines)
                               {
                                   File.WriteAllLines(path, lines);
                                   return {|E128064:File.ReadAllLines(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierA_WriteAllLines_ReadAllLines_Async_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task<string[]> M(string path, string[] lines)
                               {
                                   await File.WriteAllLinesAsync(path, lines);
                                   return {|E128064:await File.ReadAllLinesAsync(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierA_AppendAllText_ReadAllText_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(string path, string content)
                               {
                                   File.AppendAllText(path, content);
                                   return {|E128064:File.ReadAllText(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierA_CrossKind_WriteText_ReadBytes_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               byte[] M(string path, string content)
                               {
                                   File.WriteAllText(path, content);
                                   return {|E128064:File.ReadAllBytes(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierB_CreateText_ReadAllText_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(string path, string content)
                               {
                                   using (var w = File.CreateText(path))
                                   {
                                       w.Write(content);
                                   }
                                   return {|E128064:File.ReadAllText(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierB_Create_ReadAllBytes_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               byte[] M(string path, byte[] bytes)
                               {
                                   using (var fs = File.Create(path))
                                   {
                                       fs.Write(bytes, 0, bytes.Length);
                                   }
                                   return {|E128064:File.ReadAllBytes(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierB_Create_ReadAllBytesAsync_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task<byte[]> M(string path, byte[] bytes)
                               {
                                   await using (var fs = File.Create(path))
                                   {
                                       await fs.WriteAsync(bytes);
                                   }
                                   return {|E128064:await File.ReadAllBytesAsync(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierB_AppendText_ReadAllText_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(string path, string content)
                               {
                                   using (var w = File.AppendText(path))
                                   {
                                       w.Write(content);
                                   }
                                   return {|E128064:File.ReadAllText(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierC_StreamWriter_StreamReader_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(string path, string content)
                               {
                                   using (var w = new StreamWriter(path))
                                   {
                                       w.Write(content);
                                   }
                                   using var r = {|E128064:new StreamReader(path)|};
                                   return r.ReadToEnd();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierC_StreamWriter_ReadAllText_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(string path, string content)
                               {
                                   using (var w = new StreamWriter(path))
                                   {
                                       w.Write(content);
                                   }
                                   return {|E128064:File.ReadAllText(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierC_FileStreamWrite_FileStreamRead_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               int M(string path, byte[] bytes, byte[] buffer)
                               {
                                   using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                                   {
                                       fs.Write(bytes, 0, bytes.Length);
                                   }
                                   using var r = {|E128064:new FileStream(path, FileMode.Open, FileAccess.Read)|};
                                   return r.Read(buffer, 0, buffer.Length);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierC_FileStreamWriteAsync_ReadAllBytesAsync_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task<byte[]> M(string path, byte[] bytes)
                               {
                                   await using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                                   {
                                       await fs.WriteAsync(bytes);
                                   }
                                   return {|E128064:await File.ReadAllBytesAsync(path)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierD_FileInfoCreateText_ReadAllTextOnFullName_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(FileInfo fi, string content)
                               {
                                   using (var w = fi.CreateText())
                                   {
                                       w.Write(content);
                                   }
                                   return {|E128064:File.ReadAllText(fi.FullName)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierD_FileInfoOpenWrite_OpenRead_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               int M(FileInfo fi, byte[] bytes, byte[] buffer)
                               {
                                   using (var fs = fi.OpenWrite())
                                   {
                                       fs.Write(bytes, 0, bytes.Length);
                                   }
                                   using var r = {|E128064:fi.OpenRead()|};
                                   return r.Read(buffer, 0, buffer.Length);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Negative_OnlyWrite_NoRead()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(string path, string content)
                               {
                                   File.WriteAllText(path, content);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Negative_OnlyRead_NoWrite()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(string path)
                               {
                                   return File.ReadAllText(path);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Negative_DifferentPaths_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(string p1, string p2, string content)
                               {
                                   File.WriteAllText(p1, content);
                                   return File.ReadAllText(p2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Negative_PathReassigned_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(string path, string other, string content)
                               {
                                   File.WriteAllText(path, content);
                                   path = other;
                                   return File.ReadAllText(path);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Negative_WriteInIf_ReadInElse_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               string M(string path, string content, bool flag)
                               {
                                   if (flag)
                                   {
                                       File.WriteAllText(path, content);
                                       return content;
                                   }
                                   else
                                   {
                                       return File.ReadAllText(path);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Negative_UserDefinedFileType_NoReport()
    {
        return VerifyAsync("""
                           class File
                           {
                               public static void WriteAllText(string path, string content) { }
                               public static string ReadAllText(string path) => "";
                           }
                           class C
                           {
                               string M(string path, string content)
                               {
                                   File.WriteAllText(path, content);
                                   return File.ReadAllText(path);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Negative_ReadWriteAcrossMethods_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void Write(string p, string s) { File.WriteAllText(p, s); }
                               string Read(string p) { return File.ReadAllText(p); }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierE_FetchToDiskAsync_ReadAllTextAsync_ResultProperty_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading;
                           using System.Threading.Tasks;
                           class FetchResult { public FileInfo HtmlFilePath { get; init; } = null!; }
                           interface IFetcher { Task<FetchResult> FetchToDiskAsync(string url, CancellationToken ct); }
                           class C
                           {
                               async Task<string> M(IFetcher fetcher, string url, CancellationToken ct)
                               {
                                   var fetchResult = await fetcher.FetchToDiskAsync(url, ct);
                                   return {|E128064:await File.ReadAllTextAsync(fetchResult.HtmlFilePath.FullName, ct)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TierE_SaveToDisk_ReadAllBytes_ResultProperty_Flags()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class SaveResult { public string FilePath { get; init; } = ""; }
                           interface ISaver { Task<SaveResult> SaveToDiskAsync(byte[] data); }
                           class C
                           {
                               async Task<byte[]> M(ISaver saver, byte[] data)
                               {
                                   var result = await saver.SaveToDiskAsync(data);
                                   return {|E128064:await File.ReadAllBytesAsync(result.FilePath)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Negative_TierE_GenericMethodName_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class FetchResult { public string HtmlFilePath { get; init; } = ""; }
                           interface IFetcher { Task<FetchResult> FetchAsync(string url); }
                           class C
                           {
                               async Task<string> M(IFetcher fetcher, string url)
                               {
                                   var fetchResult = await fetcher.FetchAsync(url);
                                   return await File.ReadAllTextAsync(fetchResult.HtmlFilePath);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Negative_TierE_DifferentResultVariable_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class FetchResult { public string HtmlFilePath { get; init; } = ""; }
                           interface IFetcher { Task<FetchResult> FetchToDiskAsync(string url); }
                           class C
                           {
                               async Task<string> M(IFetcher fetcher, string url, string otherPath)
                               {
                                   var fetchResult = await fetcher.FetchToDiskAsync(url);
                                   return await File.ReadAllTextAsync(otherPath);
                               }
                           }
                           """);
    }
}
