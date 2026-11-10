using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RecursiveDeleteRootAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<RecursiveDeleteRootAnalyzer>(code, expected);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParameterPath_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   {|E128184:Directory.Delete(path, true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FieldReadPath_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               private readonly string root = "C:/data";

                               void M()
                               {
                                   {|E128184:Directory.Delete(root, true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyReadPath_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               private string Root { get; set; } = "C:/data";

                               void M()
                               {
                                   {|E128184:Directory.Delete(Root, true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CombineWithParameter_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   {|E128184:Directory.Delete(Path.Combine(path, "cache"), true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DirectoryInfoDeleteOnParameter_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(DirectoryInfo directory)
                               {
                                   {|E128184:directory.Delete(true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CombineWithProperty_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               private string Root { get; set; } = "C:/data";

                               void M()
                               {
                                   {|E128184:Directory.Delete(Path.Combine(Root, "cache"), true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonRecursiveDelete_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   Directory.Delete(path, false);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TempPathRoot_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   Directory.Delete(Path.GetTempPath(), true);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CombineUnderTempPath_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   Directory.Delete(Path.Combine(Path.GetTempPath(), "cache"), true);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RelativeStringLiteral_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   Directory.Delete("data/cache", true);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RootedStringLiteral_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   {|E128184:Directory.Delete("/etc/nginx", true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DriveRootedStringLiteral_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   {|E128184:Directory.Delete(@"C:\data", true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetFullPathOfTempPath_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   Directory.Delete(Path.GetFullPath(Path.GetTempPath()), true);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StartsWithGuard_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   if (path.StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
                                   {
                                       Directory.Delete(path, true);
                                   }

                                   if (Path.GetFullPath(path).StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
                                   {
                                       Directory.Delete(path, true);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CultureSensitiveGuard_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   if (path.StartsWith(Path.GetTempPath(), StringComparison.CurrentCulture))
                                   {
                                       {|E128184:Directory.Delete(path, true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuardWithoutComparisonOverload_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   if (path.StartsWith(Path.GetTempPath()))
                                   {
                                       {|E128184:Directory.Delete(path, true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SeparatorBoundedLiteralGuard_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   if (path.StartsWith("/srv/app/data/", StringComparison.Ordinal))
                                   {
                                       Directory.Delete(path, true);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SeparatorBoundedRootWithDirectorySeparatorChar_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   if (path.StartsWith("/srv/app/data" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                                   {
                                       Directory.Delete(path, true);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrefixWithoutSeparatorBoundary_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   if (path.StartsWith("/srv/app/data", StringComparison.Ordinal))
                                   {
                                       {|E128184:Directory.Delete(path, true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalSafeRootGuard_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   var temp = Path.GetTempPath();

                                   if (path.StartsWith(temp, StringComparison.Ordinal))
                                   {
                                       Directory.Delete(path, true);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadOnlyFieldGuardRoot_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           class C
                           {
                               private readonly string root = "C:/data/";

                               void M(string path)
                               {
                                   if (path.StartsWith(root, StringComparison.Ordinal))
                                   {
                                       {|E128184:Directory.Delete(path, true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetOnlyPropertyGuardRoot_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           class C
                           {
                               private string Root => "C:/data/";

                               void M(string path)
                               {
                                   if (path.StartsWith(Root, StringComparison.Ordinal))
                                   {
                                       {|E128184:Directory.Delete(path, true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalAliasOfParameter_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   var target = path;

                                   {|E128184:Directory.Delete(target, true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReassignedAfterGuard_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           class C
                           {
                               void M(string path, string other)
                               {
                                   if (path.StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
                                   {
                                       path = other;

                                       {|E128184:Directory.Delete(path, true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NegatedGuardAroundDelete_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   if (!path.StartsWith(Path.GetTempPath()))
                                   {
                                       {|E128184:Directory.Delete(path, true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuardOrCondition_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(string path, bool force)
                               {
                                   if (path.StartsWith(Path.GetTempPath(), System.StringComparison.Ordinal) || force)
                                   {
                                       {|E128184:Directory.Delete(path, true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrecedingGuardWithoutExit_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   if (path.StartsWith(Path.GetTempPath()))
                                   {
                                       System.Console.WriteLine(path);
                                   }

                                   {|E128184:Directory.Delete(path, true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrecedingNegatedGuardThatReturns_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   if (!path.StartsWith(Path.GetTempPath(), System.StringComparison.Ordinal))
                                   {
                                       return;
                                   }

                                   Directory.Delete(path, true);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MutablePropertyAsGuardRoot_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               public string Root { get; set; } = "";

                               void M(string path)
                               {
                                   if (path.StartsWith(Root, System.StringComparison.Ordinal))
                                   {
                                       {|E128184:Directory.Delete(path, true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FilesystemRootLiteral_Fires()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   {|E128184:Directory.Delete("/", true)|};
                               }
                           }
                           """);
    }
}
