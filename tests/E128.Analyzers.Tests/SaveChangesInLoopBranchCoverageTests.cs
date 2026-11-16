using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SaveChangesInLoopBranchCoverageTests
{
    private const string EfStub = """
        namespace Microsoft.EntityFrameworkCore
        {
            public class DbContext
            {
                public virtual int SaveChanges() => 0;
            }

            public class DbSet<T>
            {
            }
        }
        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<SaveChangesInLoopAnalyzer>(code + "\n" + EfStub);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenADerivedContextCallsSaveChangesWithoutAReceiver()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;

            class AppContext : DbContext
            {
                void Run(List<int> rows)
                {
                    foreach (var row in rows)
                    {
                        {|E128185:SaveChanges()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenSaveChangesIsADelegateField()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;

            class Holder
            {
                public Func<int> SaveChanges = () => 0;
            }

            class C
            {
                void M(Holder holder, List<int> rows, DbContext db)
                {
                    foreach (var row in rows)
                    {
                        _ = holder.SaveChanges();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenAnExtensionMethodTakesAContextReceiver()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;

            static class Extensions
            {
                public static int SaveChanges(this DbContext context, int retries) => retries;
            }

            class C
            {
                void M(DbContext db, List<int> rows)
                {
                    foreach (var row in rows)
                    {
                        {|E128185:db.SaveChanges(3)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenAnExtensionMethodTakesAnEntityFrameworkTypeThatIsNotAContext()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;

            static class Extensions
            {
                public static int SaveChanges<T>(this DbSet<T> set) => 0;
            }

            class C
            {
                void M(DbSet<int> set, List<int> rows)
                {
                    foreach (var row in rows)
                    {
                        {|E128185:set.SaveChanges()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnExtensionMethodAndItsReceiverAreNotEntityFramework()
    {
        return VerifyAsync("""
            using System.Collections.Generic;

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
                        _ = rows.SaveChanges();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNameIsNotAnInvocationOfAMemberOrName()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;

            class C
            {
                void M(Func<Func<int>> factory, List<int> rows, DbContext db)
                {
                    foreach (var row in rows)
                    {
                        _ = factory()();
                    }
                }
            }
            """);
    }
}
