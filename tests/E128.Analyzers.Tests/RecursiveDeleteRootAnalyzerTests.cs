using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RecursiveDeleteRootAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<RecursiveDeleteRootAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
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
    public Task StringLiteral_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   Directory.Delete("C:/data", true);
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
                           using System.IO;
                           class C
                           {
                               void M(string path)
                               {
                                   if (path.StartsWith(Path.GetTempPath()))
                                   {
                                       Directory.Delete(path, true);
                                   }

                                   if (Path.GetFullPath(path).StartsWith(Path.GetTempPath()))
                                   {
                                       Directory.Delete(path, true);
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
                                   if (path.StartsWith(Path.GetTempPath()) || force)
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
                                   if (!path.StartsWith(Path.GetTempPath()))
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
                                   if (path.StartsWith(Root))
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
