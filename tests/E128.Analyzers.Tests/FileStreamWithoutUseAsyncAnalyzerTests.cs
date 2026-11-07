using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileStreamWithoutUseAsyncAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected) =>
        AnalyzerTestRunner.VerifyAsync<FileStreamWithoutUseAsyncAnalyzer>(code, expected);

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorWithNoUseAsyncParameter_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               static void M(string path)
                               {
                                   using var stream = new FileStream(path, FileMode.Open);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseAsyncFalse_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               static void M(string path)
                               {
                                   using var stream = {|E128190:new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, false)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseAsyncNamedFalse_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               static void M(string path)
                               {
                                   using var stream = {|E128190:new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: false)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseAsyncTrue_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               static void M(string path)
                               {
                                   using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonConstantUseAsync_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               static void M(string path, bool useAsync)
                               {
                                   using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileOptionsNone_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               static void M(string path)
                               {
                                   using var stream = {|E128190:new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.None)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileOptionsAsynchronous_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               static void M(string path)
                               {
                                   using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileOptionsAsynchronousCombinedWithAnotherFlag_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               static void M(string path)
                               {
                                   using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileOptionsNamedAsynchronous_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               static void M(string path)
                               {
                                   using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, options: FileOptions.Asynchronous);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonConstantFileOptions_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               static void M(string path, FileOptions options)
                               {
                                   using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, options);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SafeFileHandleOverload_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.IO;
                           using Microsoft.Win32.SafeHandles;
                           class C
                           {
                               static void M(SafeFileHandle handle)
                               {
                                   using var stream = new FileStream(handle, FileAccess.Read);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UserTypeNamedFileStream_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.IO;
                           class FileStream
                           {
                               public FileStream(string path)
                               {
                               }
                           }
                           class C
                           {
                               static void M(string path)
                               {
                                   var stream = new FileStream(path);
                               }
                           }
                           """);
    }
}
