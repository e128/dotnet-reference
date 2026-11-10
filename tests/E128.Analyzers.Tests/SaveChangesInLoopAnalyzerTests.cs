using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SaveChangesInLoopAnalyzerTests
{
    private const string EfStub = """
                                  namespace Microsoft.EntityFrameworkCore
                                  {
                                      public class DbContext
                                      {
                                          public virtual int SaveChanges() => 0;

                                          public virtual System.Threading.Tasks.Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
                                              => System.Threading.Tasks.Task.FromResult(0);
                                      }
                                  }
                                  """;

    private const string EfExtensionStub = """
                                           namespace Microsoft.EntityFrameworkCore
                                           {
                                               public static class EntityFrameworkQueryableExtensions
                                               {
                                                   public static int SaveChanges<T>(this System.Collections.Generic.IEnumerable<T> source) => 0;
                                               }
                                           }
                                           """;

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<SaveChangesInLoopAnalyzer>(code, expected);
    }

    private static Task VerifyWithEfAsync(string code, params DiagnosticResult[] expected)
    {
        return VerifyAsync(code + "\n" + EfStub, expected);
    }

    private static Task VerifyWithEfExtensionAsync(string code, params DiagnosticResult[] expected)
    {
        return VerifyAsync(code + "\n" + EfStub + "\n" + EfExtensionStub, expected);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SaveChanges_InForeach_OnDerivedDbContext_Fires()
    {
        return VerifyWithEfAsync("""
                                 using System.Collections.Generic;
                                 using Microsoft.EntityFrameworkCore;

                                 class AppDb : DbContext
                                 {
                                     public override int SaveChanges() => 0;

                                     public void SaveAll(List<int> rows)
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
    public Task SaveChanges_InForLoop_Fires()
    {
        return VerifyWithEfAsync("""
                                 using System.Collections.Generic;
                                 using Microsoft.EntityFrameworkCore;

                                 class C
                                 {
                                     void M(DbContext db, List<int> rows)
                                     {
                                         for (int i = 0; i < rows.Count; i++)
                                         {
                                             {|E128185:db.SaveChanges()|};
                                         }
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SaveChanges_InWhileLoop_Fires()
    {
        return VerifyWithEfAsync("""
                                 using System.Collections.Generic;
                                 using Microsoft.EntityFrameworkCore;

                                 class C
                                 {
                                     void M(DbContext db, List<int> rows)
                                     {
                                         int i = 0;
                                         while (i < rows.Count)
                                         {
                                             {|E128185:db.SaveChanges()|};
                                             i++;
                                         }
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SaveChanges_InDoWhileLoop_Fires()
    {
        return VerifyWithEfAsync("""
                                 using System.Collections.Generic;
                                 using Microsoft.EntityFrameworkCore;

                                 class C
                                 {
                                     void M(DbContext db, List<int> rows)
                                     {
                                         int i = 0;
                                         do
                                         {
                                             {|E128185:db.SaveChanges()|};
                                             i++;
                                         } while (i < rows.Count);
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SaveChangesAsync_InForeach_Fires()
    {
        return VerifyWithEfAsync("""
                                 using System.Collections.Generic;
                                 using System.Threading.Tasks;
                                 using Microsoft.EntityFrameworkCore;

                                 class C
                                 {
                                     async Task M(DbContext db, List<int> rows)
                                     {
                                         foreach (var row in rows)
                                         {
                                             await {|E128185:db.SaveChangesAsync()|};
                                         }
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullConditionalSaveChanges_InForeach_Fires()
    {
        return VerifyWithEfAsync("""
                                 using System.Collections.Generic;
                                 using Microsoft.EntityFrameworkCore;

                                 class C
                                 {
                                     void M(DbContext db, List<int> rows)
                                     {
                                         foreach (var row in rows)
                                         {
                                             {|E128185:db?.SaveChanges()|};
                                         }
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExtensionSaveChanges_InForeach_Fires()
    {
        return VerifyWithEfExtensionAsync("""
                                          using System.Collections.Generic;
                                          using Microsoft.EntityFrameworkCore;

                                          class C
                                          {
                                              void M(List<int> rows)
                                              {
                                                  foreach (var row in rows)
                                                  {
                                                      {|E128185:rows.SaveChanges()|};
                                                  }
                                              }
                                          }
                                          """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SaveChanges_OutsideLoop_DoesNotFire()
    {
        return VerifyWithEfAsync("""
                                 using Microsoft.EntityFrameworkCore;

                                 class C
                                 {
                                     void M(DbContext db)
                                     {
                                         db.SaveChanges();
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SaveChanges_InForeachSourceExpression_DoesNotFire()
    {
        return VerifyWithEfAsync("""
                                 using System.Collections.Generic;
                                 using System.Linq;
                                 using Microsoft.EntityFrameworkCore;

                                 class C
                                 {
                                     void M(DbContext db)
                                     {
                                         foreach (var row in Enumerable.Repeat(db.SaveChanges(), 3))
                                         {
                                         }
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SaveChanges_InForInitializer_DoesNotFire()
    {
        return VerifyWithEfAsync("""
                                 using Microsoft.EntityFrameworkCore;

                                 class C
                                 {
                                     void M(DbContext db)
                                     {
                                         for (int i = db.SaveChanges(); i < 3; i++)
                                         {
                                         }
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonEfDbContext_InForeach_DoesNotFire()
    {
        return VerifyWithEfAsync("""
                                 using System.Collections.Generic;
                                 using Microsoft.EntityFrameworkCore;

                                 namespace Other
                                 {
                                     public class DbContext
                                     {
                                         public int SaveChanges() => 0;
                                     }
                                 }

                                 class C
                                 {
                                     void M(Other.DbContext db, List<int> rows)
                                     {
                                         foreach (var row in rows)
                                         {
                                             db.SaveChanges();
                                         }
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WithoutEntityFrameworkReference_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           class AppDb
                           {
                               public int SaveChanges() => 0;
                           }

                           class C
                           {
                               void M(AppDb db, List<int> rows)
                               {
                                   foreach (var row in rows)
                                   {
                                       db.SaveChanges();
                                   }
                               }
                           }
                           """);
    }
}
