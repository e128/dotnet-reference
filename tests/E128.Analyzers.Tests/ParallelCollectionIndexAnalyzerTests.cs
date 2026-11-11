using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ParallelCollectionIndexAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ParallelCollectionIndexAnalyzer_ReportsIndex_WhenLengthGuardIsAbsent()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelCollectionIndexAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Linq;

                           public static class Subject
                           {
                               public static void Verify(List<string> columns, List<List<string>> rows)
                               {
                                   var cells = Enumerable.Range(0, columns.Count)
                                       .Select(i => {|E128106:rows[i]|})
                                       .ToList();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParallelCollectionIndexAnalyzer_ReportsIndex_WhenTheForLoopBoundComesFromAnotherCollection()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelCollectionIndexAnalyzer>("""
                           using System.Collections.Generic;

                           public static class Subject
                           {
                               public static void Verify(List<string> columns, List<List<string>> rows)
                               {
                                   for (var i = 0; i < columns.Count; i++)
                                   {
                                       var row = {|E128106:rows[i]|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParallelCollectionIndexAnalyzer_ReportsNothing_WhenLengthGuardIsPresent()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelCollectionIndexAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Linq;

                           public static class Subject
                           {
                               public static void Verify(List<string> columns, List<List<string>> rows)
                               {
                                   var numeric = Enumerable.Range(0, columns.Count)
                                       .Where(i => rows.All(r => r.Count > i && long.TryParse(r[i], out _)))
                                       .ToList();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParallelCollectionIndexAnalyzer_ReportsNothing_WhenTheForLoopCarriesALengthGuard()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelCollectionIndexAnalyzer>("""
                           using System.Collections.Generic;

                           public static class Subject
                           {
                               public static void Verify(List<string> columns, List<List<string>> rows)
                               {
                                   for (var i = 0; i < columns.Count; i++)
                                   {
                                       var row = rows.Count > i ? rows[i] : new List<string>();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParallelCollectionIndexAnalyzer_ReportsNothing_WhenTheIndexAndTheTargetShareOneCollection()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelCollectionIndexAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Linq;

                           public static class Subject
                           {
                               public static void Verify(List<string> columns)
                               {
                                   var values = Enumerable.Range(0, columns.Count)
                                       .Select(i => columns[i])
                                       .ToList();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParallelCollectionIndexAnalyzer_ReportsNothing_WhenTheLengthGuardUsesAQualifiedReceiver()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelCollectionIndexAnalyzer>("""
                           using System.Collections.Generic;

                           public class Subject
                           {
                               private readonly List<string> _rows = new List<string>();

                               public void Verify(List<string> columns)
                               {
                                   for (var i = 0; i < columns.Count; i++)
                                   {
                                       var row = this._rows.Count > i ? this._rows[i] : string.Empty;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParallelCollectionIndexAnalyzer_ReportsNothing_WhenTheForLoopIteratesTheIndexedCollection()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelCollectionIndexAnalyzer>("""
                           using System.Collections.Generic;

                           public static class Subject
                           {
                               public static void Verify(List<string> columns)
                               {
                                   for (var i = 0; i < columns.Count; i++)
                                   {
                                       var value = columns[i];
                                   }
                               }
                           }
                           """);
    }
}
