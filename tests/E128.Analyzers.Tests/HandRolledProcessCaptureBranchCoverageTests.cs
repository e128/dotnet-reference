using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the launch shapes that bind a redirect flag to the process that waits.
/// </summary>
public sealed class HandRolledProcessCaptureBranchCoverageTests
{
    private static Task VerifyAsync(string source)
    {
        return AnalyzerTestRunner.VerifyAsync<HandRolledProcessCaptureAnalyzer>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenStartInfoIsAssignedToTheProcessMember()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   using var process = new Process();
                                   process.StartInfo = new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   };
                                   process.Start();
                                   string output = process.StandardOutput.ReadToEnd();
                                   {|E128223:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenStartInfoIsSetInTheProcessInitializer()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   using var process = new Process
                                   {
                                       StartInfo = new ProcessStartInfo(file)
                                       {
                                           RedirectStandardOutput = true,
                                       },
                                   };
                                   process.Start();
                                   string output = process.StandardOutput.ReadToEnd();
                                   {|E128223:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheProcessInitializerIsAssignedToAnExistingLocal()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   Process process;
                                   process = new Process
                                   {
                                       StartInfo = new ProcessStartInfo(file)
                                       {
                                           RedirectStandardError = true,
                                       },
                                   };
                                   process.Start();
                                   string error = process.StandardError.ReadToEnd();
                                   {|E128223:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenProcessStartResultIsAssignedToAnExistingLocal()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   Process? process;
                                   process = Process.Start(new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   });
                                   string output = process!.StandardOutput.ReadToEnd();
                                   {|E128223:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStartInfoCreationIsParenthesized()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   var process = Process.Start((new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   }));
                                   string output = process!.StandardOutput.ReadToEnd();
                                   {|E128223:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStartInfoLocalIsAssignedAfterCreation()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   using var process = new Process();
                                   var info = new ProcessStartInfo(file);
                                   info.RedirectStandardError = true;
                                   process.StartInfo = info;
                                   process.Start();
                                   string error = process.StandardError.ReadToEnd();
                                   {|E128223:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheProcessIsAField_ReachedThroughThis()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               private readonly Process process = new Process();

                               void M(string file)
                               {
                                   this.process.StartInfo.FileName = file;
                                   this.process.StartInfo.RedirectStandardOutput = true;
                                   this.process.Start();
                                   string output = this.process.StandardOutput.ReadToEnd();
                                   {|E128223:this.process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheProcessIsAStaticField_ReachedThroughItsType()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class Holder
                           {
                               internal static readonly Process Child = new Process();
                           }

                           class C
                           {
                               void M(string file)
                               {
                                   Holder.Child.StartInfo.FileName = file;
                                   Holder.Child.StartInfo.RedirectStandardOutput = true;
                                   Holder.Child.Start();
                                   string output = Holder.Child.StandardOutput.ReadToEnd();
                                   {|E128223:Holder.Child.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTargetTypedStartInfoIsPassedToStart()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   ProcessStartInfo info = new(file)
                                   {
                                       RedirectStandardOutput = true,
                                   };
                                   using var process = Process.Start(info);
                                   string output = process!.StandardOutput.ReadToEnd();
                                   {|E128223:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheWaitIsNotOnAProcess()
    {
        return VerifyAsync("""
                           class Waiter
                           {
                               public void WaitForExit()
                               {
                               }
                           }

                           class C
                           {
                               void M(Waiter waiter)
                               {
                                   waiter.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheWaitIsOnACallResult()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               Process Create() => new Process();

                               void M()
                               {
                                   Create().WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheWaitIsInAFieldInitializerLambda()
    {
        return VerifyAsync("""
                           using System;
                           using System.Diagnostics;
                           class C
                           {
                               private static readonly Process Child = new Process();

                               private static readonly Action Wait = () => Child.WaitForExit();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFlagIsNotTheLiteralTrue()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file, bool capture)
                               {
                                   using var process = new Process();
                                   process.StartInfo.FileName = file;
                                   process.StartInfo.RedirectStandardOutput = capture;
                                   process.Start();
                                   string output = process.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFlagUsesACompoundAssignment()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   using var process = new Process();
                                   process.StartInfo.FileName = file;
                                   process.StartInfo.RedirectStandardOutput |= true;
                                   process.Start();
                                   string output = process.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnElementAssignmentPrecedesTheWait()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file, bool[] flags)
                               {
                                   using var process = Process.Start(file);
                                   flags[0] = true;
                                   string output = process!.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFlagBelongsToAnotherType()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class Options
                           {
                               public bool RedirectStandardOutput { get; set; }
                           }

                           class C
                           {
                               void M(string file)
                               {
                                   var options = new Options { RedirectStandardOutput = true };
                                   options.RedirectStandardOutput = true;
                                   using var process = Process.Start(file);
                                   string output = process!.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStartInfoCreationIsNotStoredAnywhere()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               static void Use(ProcessStartInfo info)
                               {
                               }

                               void M(string file)
                               {
                                   Use(new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   });
                                   using var process = Process.Start(file);
                                   string output = process!.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStartInfoIsPassedToAnotherMethodNamedStart()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class Launcher
                           {
                               public static Process? Start(ProcessStartInfo info) => null;
                           }

                           class C
                           {
                               void M(string file)
                               {
                                   var process = Launcher.Start(new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   });
                                   string output = process!.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStartInfoIsAssignedToAnotherProcess()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   using var other = new Process();
                                   using var process = new Process();
                                   other.StartInfo = new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   };
                                   process.StartInfo = new ProcessStartInfo(file);
                                   process.Start();
                                   string output = process.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsReadThroughALocal()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   using var process = Process.Start(new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   });
                                   var reader = process!.StandardOutput;
                                   string output = reader.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReadTargetsAnotherProcess()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   using var process = Process.Start(new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   });
                                   using var other = Process.Start(file);
                                   string output = other!.StandardOutput.ReadToEnd();
                                   process!.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReadIsNotOnAStandardStream()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           using System.IO;
                           class C
                           {
                               void M(string file)
                               {
                                   using var process = Process.Start(new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   });
                                   string text = process!.StandardInput.ToString();
                                   Stream stream = process.StandardOutput.BaseStream;
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStartInfoCreationHasASuppressionOperator()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   var process = (Process.Start((new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   })!))!;
                                   string output = process.StandardOutput.ReadToEnd();
                                   {|E128223:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStartedProcessIsPassedToAnotherMethod()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               static void Use(Process? process)
                               {
                               }

                               void M(string file)
                               {
                                   var info = new ProcessStartInfo(file);
                                   info.RedirectStandardOutput = true;
                                   Use(Process.Start(info));
                                   using var process = Process.Start(file);
                                   string output = process!.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFlagReceiverIsAConditionalExpression()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file, bool first)
                               {
                                   using var left = Process.Start(file);
                                   using var right = Process.Start(file);
                                   (first ? left : right)!.StartInfo.RedirectStandardOutput = true;
                                   string output = left!.StandardOutput.ReadToEnd();
                                   left.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFlagIsSetInANestedInitializer()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   using var process = new Process
                                   {
                                       StartInfo = { FileName = file, RedirectStandardOutput = true },
                                   };
                                   process.Start();
                                   string output = process.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStartInfoIsAnArgumentOfAConstructor()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class Wrapper
                           {
                               public Wrapper(ProcessStartInfo info)
                               {
                                   Info = info;
                               }

                               public ProcessStartInfo Info { get; }
                           }

                           class C
                           {
                               void M(string file)
                               {
                                   var wrapper = new Wrapper(new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   });
                                   using var process = Process.Start(wrapper.Info);
                                   string output = process!.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStartInfoIsReturnedFromALambda()
    {
        return VerifyAsync("""
                           using System;
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   Func<ProcessStartInfo> create = () => new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   };
                                   using var process = Process.Start(create());
                                   string output = process!.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreationIsAssignedToAnExistingStartInfoLocal()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   using var process = new Process();
                                   ProcessStartInfo info;
                                   info = new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   };
                                   process.StartInfo = info;
                                   process.Start();
                                   string output = process.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreationIsAssignedToAMemberThatIsNotStartInfo()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class Box
                           {
                               public ProcessStartInfo? Info { get; set; }
                           }

                           class C
                           {
                               void M(string file)
                               {
                                   var box = new Box();
                                   box.Info = new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   };
                                   using var process = Process.Start(box.Info);
                                   string output = process!.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAReadHasNoMemberAccessReceiver()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               string ReadToEnd() => string.Empty;

                               void M(string file)
                               {
                                   using var process = Process.Start(new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                   });
                                   string text = ReadToEnd();
                                   process!.WaitForExit();
                               }
                           }
                           """);
    }
}
