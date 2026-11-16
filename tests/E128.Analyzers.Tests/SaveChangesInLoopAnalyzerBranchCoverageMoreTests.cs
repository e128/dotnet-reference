using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SaveChangesInLoopAnalyzerBranchCoverageMoreTests
{
    private const string EfStub = """
        namespace Microsoft.EntityFrameworkCore
        {
            public class DbContext
            {
                public virtual int SaveChanges() => 0;
            }
        }
        """;

    private static Task VerifyAsync(string code, bool lenient = false)
    {
        return AnalyzerTestRunner.VerifyAsync<SaveChangesInLoopAnalyzer>(
            code + "\n" + EfStub,
            options: lenient ? new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None } : null);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSaveChangesCallDoesNotResolve()
    {
        return VerifyAsync(
            """
            using System.Collections.Generic;

            class C
            {
                void M(List<int> rows)
                {
                    foreach (var row in rows)
                    {
                        Missing.SaveChanges();
                    }
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenAGenericSaveChangesOnADerivedContextRunsInALoop()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;

            class AppContext : DbContext
            {
                int SaveChanges<T>() => 0;

                void Run(List<int> rows)
                {
                    foreach (var row in rows)
                    {
                        {|E128185:SaveChanges<int>()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenANullConditionalCallContinuesWithAMemberAccess()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;

            class C
            {
                void M(DbContext db, List<int> rows)
                {
                    foreach (var row in rows)
                    {
                        db?{|E128185:.SaveChanges()|}.ToString();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenAnExtensionOnObjectIsCalledOnAContextReceiver()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;

            static class Extensions
            {
                public static int SaveChanges(this object source) => 0;
            }

            class C
            {
                void M(DbContext db, List<int> rows)
                {
                    foreach (var row in rows)
                    {
                        _ = {|E128185:db.SaveChanges()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnExtensionIsCalledAsAPlainStaticMethod()
    {
        return VerifyAsync("""
            using System.Collections.Generic;

            static class Extensions
            {
                public static int SaveChanges(this object source) => 0;

                public static void Run(object source, List<int> rows)
                {
                    foreach (var row in rows)
                    {
                        _ = SaveChanges(source);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheExtensionReceiverIsAnArray()
    {
        return VerifyAsync("""
            using System.Collections.Generic;

            static class Extensions
            {
                public static int SaveChanges(this int[] source) => 0;
            }

            class C
            {
                void M(int[] values, List<int> rows)
                {
                    foreach (var row in rows)
                    {
                        _ = values.SaveChanges();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheExtensionLivesInAnEntityFrameworkAssembly()
    {
        var test = new CSharpAnalyzerTest<SaveChangesInLoopAnalyzer, DefaultVerifier>
        {
            TestCode = """
                using System.Collections.Generic;

                namespace Other
                {
                    static class Extensions
                    {
                        public static int SaveChanges(this List<int> source) => 0;
                    }

                    class C
                    {
                        void M(List<int> rows)
                        {
                            foreach (var row in rows)
                            {
                                _ = {|E128185:rows.SaveChanges()|};
                            }
                        }
                    }
                }
                """ + "\n" + EfStub,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectAssemblyName(projectId, "Microsoft.EntityFrameworkCore.Extras"));

        return test.RunAsync(TestContext.Current.CancellationToken);
    }
}
