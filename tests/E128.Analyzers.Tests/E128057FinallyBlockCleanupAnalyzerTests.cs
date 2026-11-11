using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128057FinallyBlockCleanupAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task FinallyCleanup_Reports_WhenFileDeleteIsNotInTryCatch()
    {
        return AnalyzerTestRunner.VerifyAsync<FinallyBlockCleanupAnalyzer>("""
                           using System.IO;
                           class Processor
                           {
                               void Process(string tempPath)
                               {
                                   try { }
                                   finally
                                   {
                                       {|E128057:File.Delete(tempPath)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FinallyCleanup_NoReport_WhenFileDeleteIsInTryCatch()
    {
        return AnalyzerTestRunner.VerifyAsync<FinallyBlockCleanupAnalyzer>("""
                           using System.IO;
                           class Processor
                           {
                               void Process(string tempPath)
                               {
                                   try { }
                                   finally
                                   {
                                       try
                                       {
                                           File.Delete(tempPath);
                                       }
                                       catch (IOException) { }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FinallyCleanup_Reports_WhenDirectoryDeleteIsNotInTryCatch()
    {
        return AnalyzerTestRunner.VerifyAsync<FinallyBlockCleanupAnalyzer>("""
                           using System.IO;
                           class Processor
                           {
                               void Process(string tempPath)
                               {
                                   try { }
                                   finally
                                   {
                                       {|E128057:Directory.Delete(tempPath, true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FinallyCleanup_NoReport_WhenFinallyHasNoDangerousCall()
    {
        return AnalyzerTestRunner.VerifyAsync<FinallyBlockCleanupAnalyzer>("""
                           class Processor
                           {
                               void Process()
                               {
                                   try { }
                                   finally
                                   {
                                       var x = 1;
                                   }
                               }
                           }
                           """);
    }
}
