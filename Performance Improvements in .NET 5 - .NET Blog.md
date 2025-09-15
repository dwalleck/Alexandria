In previous releases of .NET Core, I’ve blogged about the significant performance improvements that found their way into the release. For each post, from [.NET Core 2.0](https://blogs.msdn.microsoft.com/dotnet/2017/06/07/performance-improvements-in-net-core/) to [.NET Core 2.1](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-core-2-1) to [.NET Core 3.0](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-core-3-0/), I found myself having more and more to talk about. Yet interestingly, after each I also found myself wondering whether there’d be enough meaningful improvements next time to warrant another post. Now that .NET 5 is shipping preview releases, I can definitively say the answer is, again, “yes”. .NET 5 has already seen a wealth of performance improvements, and even though it’s not scheduled for final release until [later this year](https://github.com/dotnet/core/blob/master/roadmap.md) and there’s very likely to be a lot more improvements that find their way in by then, I wanted to highlight a bunch of the improvements that are already available now. In this post, I’ll highlight ~250 pull requests that have contributed to myriad of performance improvements across .NET 5.

### Setup

[Benchmark.NET](https://github.com/dotnet/benchmarkdotnet) is now the canonical tool for measuring the performance of .NET code, making it simple to analyze the throughput and allocation of code snippets. As such, the majority of my examples in this post are measured using microbenchmarks written using that tool. To make it easy to follow-along at home (literally for many of us these days), I started by creating a directory and using the `dotnet` tool to scaffold it:

```
mkdir Benchmarks
cd Benchmarks
dotnet new console
```

and I augmented the contents of the generated Benchmarks.csproj to look like the following:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <ServerGarbageCollection>true</ServerGarbageCollection>
    <TargetFrameworks>net5.0;netcoreapp3.1;net48</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="benchmarkdotnet" Version="0.12.1" />
  </ItemGroup>

  <ItemGroup Condition=" '$(TargetFramework)' == 'net48' ">
    <PackageReference Include="System.Memory" Version="4.5.4" />
    <PackageReference Include="System.Text.Json" Version="4.7.2" />
    <Reference Include="System.Net.Http" />
  </ItemGroup>

</Project>
```

This lets me execute the benchmarks against .NET Framework 4.8, .NET Core 3.1, and .NET 5 (I currently have a [nightly build](https://github.com/dotnet/installer/blob/master/README.md#installers-and-binaries) installed for Preview 8). The .csproj also references the `Benchmark.NET` NuGet package (the latest release of which is version 12.1) in order to be able to use its features, and then references several other libraries and packages, specifically in support of being able to run tests on .NET Framework 4.8.

Then, I updated the generated Program.cs file in the same folder to look like this:

```
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;
using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

[MemoryDiagnoser]
public class Program
{
    static void Main(string[] args) => BenchmarkSwitcher.FromAssemblies(new[] { typeof(Program).Assembly }).Run(args);

    // BENCHMARKS GO HERE
}
```

and for each test, I copy/paste the benchmark code shown in each example to where it shows `"// BENCHMARKS GO HERE"`.

To run the benchmarks, I then do:

```
dotnet run -c Release -f net48 --runtimes net48 netcoreapp31 netcoreapp50 --filter ** --join
```

This tells Benchmark.NET to:

-   Build the benchmarks using the .NET Framework 4.8 surface area (which is the lowest-common denominator of all three targets and thus works for all of them).
-   Run the benchmarks against each of .NET Framework 4.8, .NET Core 3.1, and .NET 5.
-   Include all benchmarks in the assembly (don’t filter out any).
-   Join the output together from all results from all benchmarks and display that at the end of the run (rather than interspersed throughout).

In some cases where the API in question doesn’t exist for a particular target, I just leave off that part of the command-line.

Finally, a few caveats:

-   My [last benchmarks post](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-core-3-0/) was about .NET Core 3.0. I didn’t write one about .NET Core 3.1 because, from a runtime and core libraries perspective, it saw relatively few improvements over its predecessor released just a few months prior. However, there were some improvements, on top of which in some cases we’ve already back-ported improvements made for .NET 5 back to .NET Core 3.1, where the changes were deemed impactful enough to warrant being added to the Long Term Support (LTS) release. As such, all of my comparisons here are against the latest .NET Core 3.1 servicing release (3.1.5) rather than against .NET Core 3.0.
-   As the comparisons are about .NET 5 vs .NET Core 3.1, and as .NET Core 3.1 didn’t include the mono runtime, I’ve refrained from covering improvements made to mono, as well as to core library improvements specifically focused on [“Blazor”](https://devblogs.microsoft.com/aspnet/blazor-webassembly-3-2-0-now-available/). Thus when I refer to “the runtime”, I’m referring to coreclr, even though as of .NET 5 there are multiple runtimes under its umbrella, and all of them have been improved.
-   Most of my examples were run on Windows, because I wanted to be able to compare against .NET Framework 4.8 as well. However, unless otherwise mentioned, all of the examples shown accrue equally to Windows, Linux, and macOS.
-   The standard caveat: all measurements here are on my desktop machine, and your mileage may vary. Microbenchmarks can be very sensitive to any number of factors, including processor count, processor architecture, memory and cache speeds, and on and on. However, in general I’ve focused on performance improvements and included examples that should generally withstand any such differences.

Let’s get started…

## GC

For anyone interested in .NET and performance, garbage collection is frequently top of mind. Lots of effort goes into reducing allocation, not because the act of allocating is itself particularly expensive, but because of the follow-on costs in cleaning up after those allocations via the garbage collector (GC). No matter how much work goes into reducing allocations, however, the vast majority of workloads will incur them, and thus it’s important to continually push the boundaries of what the GC is able to accomplish, and how quickly.

This release has seen a lot of effort go into improving the GC. For example, [dotnet/coreclr#25986](https://github.com/dotnet/coreclr/pull/25986) implements a form of work stealing for the “mark” phase of the GC. The .NET GC is a [“tracing”](https://en.wikipedia.org/wiki/Tracing_garbage_collection) collector, meaning that (at a very high level) when it runs it starts from a set of “roots” (known locations that are inherently reachable, such as a static field) and traverses from object to object, “marking” each as being reachable; after all such traversals, any objects not marked are unreachable and can be collected. This marking represents a significant portion of the time spent performing collections, and this PR improves marking performance by better balancing the work performed by each thread involved in the collection. When running with the “Server GC”, a thread per core is involved in collections, and as threads finish their allotted portions of the marking work, they’re now able to “steal” undone work from other threads in order to help the overall collection complete more quickly.

As another example, [dotnet/runtime#35896](https://github.com/dotnet/runtime/pull/35896) optimizes decommits on the “ephemeral” segment (gen0 and gen1 are referred to as “ephemeral” because they’re objects expected to last for only a short time). Decommitting is the act of giving pages of memory back to the operating system at the end of segments after the last live object on that segment. The question for the GC then becomes, when should such decommits happen, and how much should it decommit at any point in time, given that it may end up needing to allocate additional pages for additional allocations at some point in the near future.

Or take [dotnet/runtime#32795](https://github.com/dotnet/runtime/pull/32795), which improves the GC’s scalability on machines with higher core counts by reducing lock contention involved in the GC’s scanning of statics. Or [dotnet/runtime#37894](https://github.com/dotnet/runtime/pull/37894), which avoids costly memory resets (essentially telling the OS that the relevant memory is no longer interesting) unless the GC sees it’s in a low-memory situation. Or [dotnet/runtime#37159](https://github.com/dotnet/runtime/pull/37159), which (although not yet merged, is expected to be for .NET 5) builds on the work of [@damageboy](https://github.com/damageboy) to vectorize sorting employed in the GC. Or [dotnet/coreclr#27729](https://github.com/dotnet/coreclr/pull/27729), which reduces the time it takes for the GC to suspend threads, something that’s necessary in order for it to get a stable view so that it can accurately determine which are being used.

This is only a partial list of changes made to improve the GC itself, but that last bullet brings me to a topic of particular fascination for me, as it speaks to a lot of the work we’ve done in .NET in recent years. In this release, we’ve continued, and even accelerated, the process of porting native implementations in the coreclr runtime from C/C++ to instead be normal C# managed code in System.Private.Corelib. Such a move has a plethora of benefits, including making it much easier for us to share a single implementation across multiple runtimes (like coreclr and mono), and even making it easier for us to evolve API surface area, such as by reusing the same logic to handle both arrays and spans. But one thing that takes some folks by surprise is that such benefits also include performance, in multiple ways. One such way harkens back to one of the original motivations for using a managed runtime: safety. By default, code written in C# is “safe”, in that the runtime ensures all memory accesses are bounds checked, and only by explicit action visible in the code (e.g. using the `unsafe` keyword, the `Marshal` class, the `Unsafe` class, etc.) is a developer able to remove such validation. As a result, as maintainers of an open source project, our job of shipping a secure system is made significantly easier when contributions come in the form of managed code: while such code can of course contain bugs that might slip through code reviews and automated testing, we can sleep better at night knowing that the chances for such bugs to introduce security problems are drastically reduced. That in turn means we’re more likely to accept improvements to managed code and at a higher velocity, with it being faster for a contributor to provide and faster for us to help validate. We’ve also found a larger number of contributors interested in exploring performance improvements when it comes in the form of C# rather than C. And more experimentation from more people progressing at a faster rate yields better performance.

There are, however, more direct forms of performance improvements we’ve seen from such porting. There is a relatively small amount of overhead required for managed code to call into the runtime, but when such calls are made at high frequency, such overhead adds up. Consider [dotnet/coreclr#27700](https://github.com/dotnet/coreclr/pull/27700), which moved the implementation of the sorting of arrays of primitive types out of native code in coreclr and up into C# in Corelib. In addition to that code then powering new public APIs for sorting spans, it also made it cheaper to sort smaller arrays where the cost of doing so is dominated by the transition from managed code. We can see this with a small benchmark, which is just using `Array.Sort` to sort `int[]`, `double[]`, and `string[]` arrays of 10 items:

```
public class DoubleSorting : Sorting<double> { protected override double GetNext() => _random.Next(); }
public class Int32Sorting : Sorting<int> { protected override int GetNext() => _random.Next(); }
public class StringSorting : Sorting<string>
{
    protected override string GetNext()
    {
        var dest = new char[_random.Next(1, 5)];
        for (int i = 0; i < dest.Length; i++) dest[i] = (char)('a' + _random.Next(26));
        return new string(dest);
    }
}

public abstract class Sorting<T>
{
    protected Random _random;
    private T[] _orig, _array;

    [Params(10)]
    public int Size { get; set; }

    protected abstract T GetNext();

    [GlobalSetup]
    public void Setup()
    {
        _random = new Random(42);
        _orig = Enumerable.Range(0, Size).Select(_ => GetNext()).ToArray();
        _array = (T[])_orig.Clone();
        Array.Sort(_array);
    }

    [Benchmark]
    public void Random()
    {
        _orig.AsSpan().CopyTo(_array);
        Array.Sort(_array);
    }
}
```

| Type | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| DoubleSorting | .NET FW 4.8 | 88.88 ns | 1.00 |
| DoubleSorting | .NET Core 3.1 | 73.29 ns | 0.83 |
| DoubleSorting | .NET 5.0 | 35.83 ns | 0.40 |
|  |  |  |  |
| Int32Sorting | .NET FW 4.8 | 66.34 ns | 1.00 |
| Int32Sorting | .NET Core 3.1 | 48.47 ns | 0.73 |
| Int32Sorting | .NET 5.0 | 31.07 ns | 0.47 |
|  |  |  |  |
| StringSorting | .NET FW 4.8 | 2,193.86 ns | 1.00 |
| StringSorting | .NET Core 3.1 | 1,713.11 ns | 0.78 |
| StringSorting | .NET 5.0 | 1,400.96 ns | 0.64 |

This in and of itself is a nice benefit of the move, as is the fact that in .NET 5 via [dotnet/runtime#37630](https://github.com/dotnet/runtime/pull/37630) we also added `System.Half`, a new 16-bit floating-point primitive, and being in managed code, this sorting implementation’s optimizations almost immediately applied to it, whereas the previous native implementation would have required significant additional work, with no C++ standard type for `half`. But, there’s an arguably even more impactful performance benefit here, and it brings us back to where I started this discussion: GC.

One of the interesting metrics for the GC is “pause time”, which effectively means how long the GC must pause the runtime in order to perform its work. Longer pause times have a direct impact on latency, which can be a crucial metric for all manner of workloads. As alluded to earlier, the GC may need to suspend threads in order to get a consistent view of the world and to ensure that it can move objects around safely, but if a thread is currently executing C/C++ code in the runtime, the GC may need to wait until that call completes before it’s able to suspend the thread. Thus, the more work we can do in managed code instead of native code, the better off we are for GC pause times. We can use the same `Array.Sort` example to see this. Consider this program:

```
using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    public static void Main()
    {
        new Thread(() =>
        {
            var a = new int[20];
            while (true) Array.Sort(a);
        }) { IsBackground = true }.Start();

        var sw = new Stopwatch();
        while (true)
        {
            sw.Restart();
            for (int i = 0; i < 10; i++)
            {
                GC.Collect();
                Thread.Sleep(15);
            }
            Console.WriteLine(sw.Elapsed.TotalSeconds);
        }
    }
}
```

This is spinning up a thread that just sits in a tight loop sorting a small array over and over, while on the main thread it performs 10 GCs, each with approximately 15 milliseconds between them. So, we’d expect that loop to take a little more than 150 milliseconds. But when I run this on .NET Core 3.1, I get numbers of seconds like this:

```
6.6419048
5.5663149
5.7430339
6.032052
7.8892468
```

The GC has difficulty here interrupting the thread performing the sorts, causing the GC pause times to be way higher than desirable. Thankfully, when I instead run this on .NET 5, I get numbers like this:

```
0.159311
0.159453
0.1594669
0.1593328
0.1586566
```

which is exactly what we predicted we should get. By moving the Array.Sort implementation into managed code, where the runtime can more easily suspend the implementation when it wants to, we’ve made it possible for the GC to be much better at its job.

This isn’t limited to just `Array.Sort`, of course. A bunch of PRs performed such porting, for example [dotnet/runtime#32722](https://github.com/dotnet/runtime/pull/32722) moving the `stdelemref` and `ldelemaref` JIT helpers to C#, [dotnet/runtime#32353](https://github.com/dotnet/runtime/pull/32353) moving portions of the `unbox` helper to C# (and instrumenting the rest with appropriate GC polling locations that let the GC suspend appropriately in the rest), [dotnet/coreclr#27603](https://github.com/dotnet/coreclr/pull/27603) / [dotnet/coreclr#27634](https://github.com/dotnet/coreclr/pull/27634) / [dotnet/coreclr#27123](https://github.com/dotnet/coreclr/pull/27123) / [dotnet/coreclr#27776](https://github.com/dotnet/coreclr/pull/27776) moving more array implementations like `Array.Clear` and `Array.Copy` to C#, [dotnet/coreclr#27216](https://github.com/dotnet/coreclr/pull/27216) moving more of `Buffer` to C#, and [dotnet/coreclr#27792](https://github.com/dotnet/coreclr/pull/27792) moving `Enum.CompareTo` to C#. Some of these changes then enabled subsequent gains, such as with [dotnet/runtime#32342](https://github.com/dotnet/runtime/pull/32342) and [dotnet/runtime#35733](https://github.com/dotnet/runtime/pull/35733), which employed the improvements in `Buffer.Memmove` to achieve additional gains in various `string` and `Array` methods.

As one final thought on this set of changes, another interesting thing to note is how micro-optimizations made in one release may be based on assumptions that are later invalidated, and when employing such micro-optimizations, one needs to be ready and willing to adapt. In my .NET Core 3.0 blog post, I called out “peanut butter” changes like [dotnet/coreclr#21756](https://github.com/dotnet/coreclr/pull/21756), which switched lots of call sites from using `Array.Copy(source, destination, length)` to instead use `Array.Copy(source, sourceOffset, destination, destinationOffset, length)`, because the overhead involved in the former getting the lower bounds of the source and destination arrays was measurable. But with the aforementioned set of changes that moved array-processing code to C#, the simpler overload’s overheads disappeared, making it both the simpler and faster choice for these operations. And such, for .NET 5 PRs [dotnet/coreclr#27641](https://github.com/dotnet/coreclr/pull/27641) and [dotnet/corefx#42343](https://github.com/dotnet/corefx/pull/42343) switched all of these call sites and more back to using the simpler overload. [dotnet/runtime#36304](https://github.com/dotnet/runtime/pull/36304) is another example of undoing previous optimizations due to changes that made them obsolete or actually harmful. You’ve always been able to pass a single character to `String.Split`, e.g. `version.Split('.')`. The problem, however, was the only overload of `Split` that this could bind to was `Split(params char[] separator)`, which means that every such call resulted in the C# compiler generating a `char[]` allocation. To work around that, previous releases saw caches added, allocating arrays ahead of time and storing them into statics that could then be used by `Split` calls to avoid the per-call `char[]`. Now that there’s a `Split(char separator, StringSplitOptions options = StringSplitOptions.None)` overload in .NET, we no longer need the array at all.

As one last example, I showed how moving code out of the runtime and into managed code can help with GC pauses, but there are of course other ways code remaining in the runtime can help with that. [dotnet/runtime#36179](https://github.com/dotnet/runtime/pull/36179) reduced GC pauses due to exception handling by ensuring the runtime was in [preemptive mode](https://github.com/dotnet/runtime/blob/4fdf9ff8812869dcf957ce0d2eb07c0d5779d1c6/docs/coding-guidelines/clr-code-guide.md#218-use-the-right-gc-mode--preemptive-vs-cooperative) around code such as getting “Watson” bucket parameters (basically, a set of data that uniquely identifies this particular exception and call stack for reporting purposes).

## JIT

.NET 5 is an exciting version for the Just-In-Time (JIT) compiler, too, with many improvements of all manner finding their way into the release. As with any compiler, improvements made to the JIT can have wide-reaching effects. Often individual changes have a small impact on an individual piece of code, but such changes are then magnified by the sheer number of places they apply.

There is an almost unbounded number of optimizations that can be added to the JIT, and given an unlimited amount of time to run such optimizations, the JIT could create the most optimal code for any given scenario. But the JIT doesn’t have an unbounded amount of time. The “just-in-time” nature of the JIT means it’s performing the compilation as the app runs: when a method that hasn’t yet been compiled is invoked, the JIT needs to provide the assembly code for it on-demand. That means the thread can’t make forward progress until the compilation has completed, which in turn means the JIT needs to be strategic in what optimizations it applies and how it chooses to use its limited time budget. Various techniques are used to give the JIT more time, such as using “ahead of time” compilation (AOT) on some portions of the app to do as much of the compilation work as is possible before the app is executed (for example, the core libraries are all AOT compiled using a technology named [“ReadyToRun”](https://github.com/dotnet/runtime/blob/99aae90739c2ad5642a36873334c82a8b7fb2de9/docs/design/coreclr/botr/readytorun-overview.md), which you may hear referred to as “R2R” or even “crossgen”, which is the tool that produces these images), or by using [“tiered compilation”](https://github.com/dotnet/runtime/blob/9900dfb4b2e32cf02ca846adaf11e93211629ede/docs/design/features/tiered-compilation.md), which allows the JIT to initially compile a method with few-to-no optimizations applied and thus be very fast in doing so, and only spend more time recompiling it with many more optimizations when it’s deemed valuable, namely when the method is shown to be used repeatedly. However, more generally the developers contributing to the JIT simply choose to use the allotted time budget for optimizations that prove to be valuable given the code developers are writing and the code patterns they’re employing. That means that as .NET evolves and gains new capabilities, new language features, and new library features, the JIT also evolves with optimizations suited to the newer style of code being written.

A great example of that is with [dotnet/runtime#32538](https://github.com/dotnet/runtime/pull/32538) from [@benaadams](https://github.com/benaadams). `Span<T>` has been permeating all layers of the .NET stack, as developers working on the runtime, core libraries, ASP.NET Core, and beyond recognize its power when it comes to writing safe and efficient code that also unifies handling for strings, managed arrays, natively-allocated memory, and other forms of data. Similarly, value types (structs) are being used much more pervasively as a way to avoid object allocation overheads via stack allocation. But this heavy reliance on such types also introduces additional headaches for the runtime. The coreclr runtime uses a [“precise” garbage collector](https://en.wikipedia.org/wiki/Tracing_garbage_collection#Precise_vs._conservative_and_internal_pointers), which means the GC is able to track with 100% accuracy what values refer to managed objects and what values don’t; that has benefits, but it also has cost (in contrast, the mono runtime uses a “conservative” garbage collector, which has some performance benefits, but also means it may interpret an arbitrary value on the stack that happens to be the same as a managed object’s address as being a live reference to that object). One such cost is that the JIT needs to help the GC by guaranteeing that any local that could be interpreted as an object reference is zero’d out prior to the GC paying attention to it; otherwise, the GC could end up seeing a garbage value in a local that hadn’t been set yet, and assume it referred to a valid object, at which point “bad things” can happen. The more reference locals there are, the more clearing needs to be done. If you’re just clearing a few locals, it’s probably not noticeable. But as the number increases, the amount of time spent clearing those locals can add up, especially in a small method used in a very hot code path. This situation has become much more common with spans and structs, where coding patterns often result in many more references (a `Span<T>` contains a reference) that need to be zero’d. The aforementioned PR addressed this by updating the JIT’s generated code for the prolog blocks that perform this zero’ing to use `xmm` registers rather than using the `rep stosd` instruction. Effectively, it vectorized the zeroing. You can see the impact of this with the following benchmark:

```
[Benchmark]
public int Zeroing()
{
    ReadOnlySpan<char> s1 = "hello world";
    ReadOnlySpan<char> s2 = Nop(s1);
    ReadOnlySpan<char> s3 = Nop(s2);
    ReadOnlySpan<char> s4 = Nop(s3);
    ReadOnlySpan<char> s5 = Nop(s4);
    ReadOnlySpan<char> s6 = Nop(s5);
    ReadOnlySpan<char> s7 = Nop(s6);
    ReadOnlySpan<char> s8 = Nop(s7);
    ReadOnlySpan<char> s9 = Nop(s8);
    ReadOnlySpan<char> s10 = Nop(s9);
    return s1.Length + s2.Length + s3.Length + s4.Length + s5.Length + s6.Length + s7.Length + s8.Length + s9.Length + s10.Length;
}

[MethodImpl(MethodImplOptions.NoInlining)]
private static ReadOnlySpan<char> Nop(ReadOnlySpan<char> span) => default;
```

On my machine, I get results like the following:

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Zeroing | .NET FW 4.8 | 22.85 ns | 1.00 |
| Zeroing | .NET Core 3.1 | 18.60 ns | 0.81 |
| Zeroing | .NET 5.0 | 15.07 ns | 0.66 |

Note that such zero’ing is actually needed in more situations than I mentioned. In particular, by default the C# specification requires that all locals be initialized to their default values before the developer’s code is executed. You can see this with an example like this:

```
using System;
using System.Runtime.CompilerServices;
using System.Threading;

unsafe class Program
{
    static void Main()
    {
        while (true)
        {
            Example();
            Thread.Sleep(1);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Example()
    {
        Guid g;
        Console.WriteLine(*&g);
    }
}
```

Run that, and you should see only `Guid`s of all `0`s output. That’s because the C# compiler is emitting a `.locals init` flag into the IL for the compiled `Example` method, and that `.locals init` tells the JIT it needs to zero out all locals, not just those that contain references. However, in .NET 5, there’s a new attribute in the runtime ([dotnet/runtime#454](https://github.com/dotnet/runtime/pull/454)):

```
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event | AttributeTargets.Interface, Inherited = false)]
    public sealed class SkipLocalsInitAttribute : Attribute { }
}
```

This attribute is recognized by the C# compiler and is used to tell the compiler to not emit the `.locals init` when it otherwise would have. If we make a small tweak to the previous example, adding the attribute to the whole module:

```
using System;
using System.Runtime.CompilerServices;
using System.Threading;

[module: SkipLocalsInit]

unsafe class Program
{
    static void Main()
    {
        while (true)
        {
            Example();
            Thread.Sleep(1);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Example()
    {
        Guid g;
        Console.WriteLine(*&g);
    }
}
```

you should now see different results, in particular you should very likely see non-zero `Guid`s. As of [dotnet/runtime#37541](https://github.com/dotnet/runtime/pull/37541), the core libraries in .NET 5 all use this attribute now to disable `.locals init` (in previous releases, `.locals init` was stripped out by a post-compilation step employed when building the core libraries). Note that the C# compiler only allows `SkipLocalsInit` to be used in `unsafe` contexts, because it can easily result in corruption in code that hasn’t been appropriately validated for its use (so be thoughtful if / when you apply it).

In addition to making zero’ing faster, there also have been changes to remove the zero’ing entirely. For example, [dotnet/runtime#31960](https://github.com/dotnet/runtime/pull/31960), [dotnet/runtime#36918](https://github.com/dotnet/runtime/pull/36918), [dotnet/runtime#37786](https://github.com/dotnet/runtime/pull/37786), and [dotnet/runtime#38314](https://github.com/dotnet/runtime/pull/38314) all contributed to removing zero’ing when the JIT could prove it to be duplicative.

Such zero’ing is an example of a tax incurred for managed code, with the runtime needing it in order to provide guarantees of its model and of the requirements of the languages above it. Another such tax is bounds checking. One of the great advantages of using managed code is that a whole class of potential security vulnerabilities are made irrelevant by default. The runtime ensures that indexing into arrays, strings, and spans is bounds-checked, meaning the runtime injects checks to ensure that the index being requested is within the bounds of the data being indexed (i.e. greater than or equal to zero and less then the length of the data). Here’s a simple example:

```
public static char Get(string s, int i) => s[i];
```

For this code to be safe, the runtime needs to generate a check that `i` falls within the bounds of string `s`, which the JIT does by using assembly like the following:

```
; Program.Get(System.String, Int32)
       sub       rsp,28
       cmp       edx,[rcx+8]
       jae       short M01_L00
       movsxd    rax,edx
       movzx     eax,word ptr [rcx+rax*2+0C]
       add       rsp,28
       ret
M01_L00:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 28
```

This assembly was generated via a handy feature of Benchmark.NET: add `[DisassemblyDiagnoser]` to the class containing the benchmarks, and it spits out the disassembled assembly code. We can see that the assembly takes the string (passed via the `rcx` register) and loads the string’s length (which is stored 8 bytes into the object, hence the `[rcx+8]`), comparing that with `i` passed in the `edx` register, and if with an unsigned comparison (unsigned so that any negative values wrap around to be larger than the length) `i` is greater than or equal to the length, jumping to a helper `COREINFO_HELP_RNGCHKFAIL` that throws an exception. Just a few instructions, but certain kinds of code can spend a lot of cycles indexing, and thus it’s helpful when the JIT can eliminate as many of the bounds checks as it can prove to be unnecessary.

The JIT has already been capable of removing bounds checks in a variety of situations. For example, when you write the loop:

```
int[] arr = ...;
for (int i = 0; i < arr.Length; i++)
    Use(arr[i]);
```

the JIT can prove that `i` will never be outside the bounds of the array, and so it can elide the bounds checks it would otherwise generate. In .NET 5, it can remove bounds checking in more places. For example, consider this function that writes the bytes of an integer as characters to a span:

```
private static bool TryToHex(int value, Span<char> span)
{
    if ((uint)span.Length <= 7)
        return false;

    ReadOnlySpan<byte> map = new byte[] { (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' }; ;
    span[0] = (char)map[(value >> 28) & 0xF];
    span[1] = (char)map[(value >> 24) & 0xF];
    span[2] = (char)map[(value >> 20) & 0xF];
    span[3] = (char)map[(value >> 16) & 0xF];
    span[4] = (char)map[(value >> 12) & 0xF];
    span[5] = (char)map[(value >> 8) & 0xF];
    span[6] = (char)map[(value >> 4) & 0xF];
    span[7] = (char)map[value & 0xF];
    return true;
}

private char[] _buffer = new char[100];

[Benchmark]
public bool BoundsChecking() => TryToHex(int.MaxValue, _buffer);
```

First, in this example it’s worth noting we’re relying on a C# compiler optimization. Note the:

```
ReadOnlySpan<byte> map = new byte[] { (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' };
```

That looks terribly expensive, like we’re allocating a byte array on each call to `TryToHex`. In fact, it’s not, and it’s actually better than if we had done:

```
private static readonly byte[] s_map = new byte[] { (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' };
...
ReadOnlySpan<byte> map = s_map;
```

The C# compiler recognizes the pattern of a new byte array being assigned directly to a `ReadOnlySpan<byte>` (it also recognizes `sbyte` and `bool`, but nothing larger than a byte because of endianness concerns). Because the array nature is then completely hidden by the span, the C# compiler emits that by actually storing the bytes into the assembly’s data section, and the span is just created by wrapping it around a pointer to the static data and the length:

```
IL_000c: ldsflda valuetype '<PrivateImplementationDetails>'/'__StaticArrayInitTypeSize=16' '<PrivateImplementationDetails>'::'2125B2C332B1113AAE9BFC5E9F7E3B4C91D828CB942C2DF1EEB02502ECCAE9E9'
IL_0011: ldc.i4.s 16
IL_0013: newobj instance void valuetype [System.Runtime]System.ReadOnlySpan'1<uint8>::.ctor(void*, int32)
```

This is important for this JIT discussion, because of that `ldc.i4.s 16` in the above. That’s the IL loading the length of 16 to use to create the span, and the JIT can see that. It knows then that the span has a length of 16, which means if it can prove that an access is always to a value greater than or equal to 0 and less than 16, it needn’t bounds check that access. [dotnet/runtime#1644](https://github.com/dotnet/runtime/pull/1644) did exactly that, recognizing patterns like `array[index % const]`, and eliding the bounds check when the `const` was less than or equal to the length. In the previous `TryToHex` example, the JIT can see that the `map` span has a length of 16, and it can see that all of the indexing into it is done with `& 0xF`, meaning all values will end up being in range, and thus it can eliminate all of the bounds checks on `map`. Combine that with the fact that it could already see that no bounds checking is needed on the writes into the `span` (because it could see the length check earlier in the method guarded all of the indexing into `span`), and this whole method is bounds-check-free in .NET 5. On my machine, this benchmark yields results like the following:

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| BoundsChecking | .NET FW 4.8 | 14.466 ns | 1.00 | 830 B |
| BoundsChecking | .NET Core 3.1 | 4.264 ns | 0.29 | 320 B |
| BoundsChecking | .NET 5.0 | 3.641 ns | 0.25 | 249 B |

Note the .NET 5 run is not only 15% faster than the .NET Core 3.1 run, we can see its assembly code size is 22% smaller (the extra “Code Size” column comes from my having added `[DisassemblyDiagnoser]` to the benchmark class).

Another nice bounds checking removal comes from [@nathan-moore](https://github.com/nathan-moore) in [dotnet/runtime#36263](https://github.com/dotnet/runtime/pull/36263). I mentioned that the JIT is already able to remove bounds checking for the very common pattern of iterating from 0 to the array, string, or span’s length, but there are variations on this that are also relatively common but that weren’t previously recognized. For example, consider this microbenchmark which calls a method that detects whether a span of integers is sorted:

```
private int[] _array = Enumerable.Range(0, 1000).ToArray();

[Benchmark]
public bool IsSorted() => IsSorted(_array);

private static bool IsSorted(ReadOnlySpan<int> span)
{
    for (int i = 0; i < span.Length - 1; i++)
        if (span[i] > span[i + 1])
            return false;

    return true;
}
```

This slight variation from the recognized pattern was enough previously to prevent the JIT from eliding the bounds checks. Not anymore. .NET 5 on my machine is able to execute this 20% faster:

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| IsSorted | .NET FW 4.8 | 1,083.8 ns | 1.00 | 236 B |
| IsSorted | .NET Core 3.1 | 581.2 ns | 0.54 | 136 B |
| IsSorted | .NET 5.0 | 463.0 ns | 0.43 | 105 B |

Another case where the JIT ensures checks are in place for a category of error are null checks. The JIT does this in coordination with the runtime, with the JIT ensuring appropriate instructions are in place to incur hardware exceptions and with the runtime then translating such faults into .NET exceptions (e.g. [here](https://github.com/dotnet/runtime/blob/9df02475e09859a8d24852011cf3515f7a665670/src/coreclr/src/vm/excep.cpp#L3073)). But sometimes instructions are necessary only for null checks rather than also accomplishing other necessary functionality, and as long as the required null check happens due to some instruction, the unnecessary duplicative ones can be removed. Consider this code:

```
private (int i, int j) _value;

[Benchmark]
public int NullCheck() => _value.j++;
```

As a runnable benchmark, this does too little work to accurately measure with Benchmark.NET, but it’s a great way to see what assembly code is generated. With .NET Core 3.1, this method results in this assembly:

```
; Program.NullCheck()
       nop       dword ptr [rax+rax]
       cmp       [rcx],ecx
       add       rcx,8
       add       rcx,4
       mov       eax,[rcx]
       lea       edx,[rax+1]
       mov       [rcx],edx
       ret
; Total bytes of code 23
```

That `cmp [rcx],ecx` instruction is performing a null check on `this` as part of calculating the address of `j`. Then the `mov eax,[rcx]` instruction is performing another null check as part of dereferencing `j`‘s location. That first null check is thus not actually necessary, with the instruction not providing any other benefits. So, thanks to PRs like [dotnet/runtime#1735](https://github.com/dotnet/runtime/pull/1735) and [dotnet/runtime#32641](https://github.com/dotnet/runtime/pull/32641), such duplication is recognized by the JIT in many more cases than before, and for .NET 5 we now end up with:

```
; Program.NullCheck()
       add       rcx,0C
       mov       eax,[rcx]
       lea       edx,[rax+1]
       mov       [rcx],edx
       ret
; Total bytes of code 12
```

Covariance is another case where the JIT needs to inject checks to ensure that a developer can’t accidentally break type or memory safety. Consider code like:

```
class A { }
class B { }
object[] arr = ...;
arr[0] = new A();
```

Is this code valid? It depends. Arrays in .NET are “covariant”, which means I can pass around an array `DerivedType[]` as a `BaseType[]`, where `DerivedType` derives from `BaseType`. That means in this example, the `arr` could have been constructed as `new A[1]` or `new object[1]` or `new B[1]`. This code should run fine with the first two, but if the `arr` is actually a `B[]`, trying to store an `A` instance into it must fail; otherwise, code that’s using the array as a `B[]` could try to use `B[0]` as a `B` and things could go badly quickly. So, the runtime needs to protect against this by doing covariance checking, which really means when a reference type instance is stored into an array, the runtime needs to check that the assigned type is in fact compatible with the concrete type of the array. With [dotnet/runtime#189](https://github.com/dotnet/runtime/pull/189), the JIT is now able to eliminate more covariance checks, specifically in the case where the element type of the array is sealed, like `string`. As a result of this, a microbenchmark like this now runs faster:

```
private string[] _array = new string[1000];

[Benchmark]
public void CovariantChecking()
{
    string[] array = _array;
    for (int i = 0; i < array.Length; i++)
        array[i] = "default";
}
```

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| CovariantChecking | .NET FW 4.8 | 2.121 us | 1.00 | 57 B |
| CovariantChecking | .NET Core 3.1 | 2.122 us | 1.00 | 57 B |
| CovariantChecking | .NET 5.0 | 1.666 us | 0.79 | 52 B |

Related to this are type checks. I mentioned earlier that `Span<T>` solved a bunch of problems but also introduced new patterns that then drove improvements in other areas of the system; that goes as well for the implementation of `Span<T>` itself. `Span<T>`‘s constructor does a covariance check that requires a `T[]` to actually be a `T[]` and not a `U[]` where `U` derives from `T`, e.g. this program:

```
using System;

class Program
{
    static void Main() => new Span<A>(new B[42]);
}

class A { }
class B : A { }
```

will result in an exception:

```
System.ArrayTypeMismatchException: Attempted to access an element as a type incompatible with the array.
```

That exception stems from [this check](https://github.com/dotnet/runtime/blob/f170db722be6fb695ca229bcbe46be0caa8b3a48/src/libraries/System.Private.CoreLib/src/System/Span.cs#L46-L47) in `Span<T>`‘s constructor:

```
if (!typeof(T).IsValueType && array.GetType() != typeof(T[]))
    ThrowHelper.ThrowArrayTypeMismatchException();
```

PR [dotnet/runtime#32790](https://github.com/dotnet/runtime/pull/32790) optimized just such a `array.GetType() != typeof(T[])` check when `T` is sealed, while [dotnet/runtime#1157](https://github.com/dotnet/runtime/pull/1157) recognizes the `typeof(T).IsValueType` pattern and replaces it with a constant value (PR [dotnet/runtime#1195](https://github.com/dotnet/runtime/pull/1195) does the same for `typeof(T1).IsAssignableFrom(typeof(T2))`). The net effect of that is huge improvement on a microbenchmark like this:

```
class A { }
sealed class B : A { }

private B[] _array = new B[42];

[Benchmark]
public int Ctor() => new Span<B>(_array).Length;
```

for which I get results like:

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| Ctor | .NET FW 4.8 | 48.8670 ns | 1.00 | 66 B |
| Ctor | .NET Core 3.1 | 7.6695 ns | 0.16 | 66 B |
| Ctor | .NET 5.0 | 0.4959 ns | 0.01 | 17 B |

The explanation of the difference is obvious when looking at the generated assembly, even when not completely versed in assembly code. Here’s what the `[DisassemblyDiagnoser]` shows was generated on .NET Core 3.1:

```
; Program.Ctor()
       push      rdi
       push      rsi
       sub       rsp,28
       mov       rsi,[rcx+8]
       test      rsi,rsi
       jne       short M00_L00
       xor       eax,eax
       jmp       short M00_L01
M00_L00:
       mov       rcx,rsi
       call      System.Object.GetType()
       mov       rdi,rax
       mov       rcx,7FFE4B2D18AA
       call      CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE
       cmp       rdi,rax
       jne       short M00_L02
       mov       eax,[rsi+8]
M00_L01:
       add       rsp,28
       pop       rsi
       pop       rdi
       ret
M00_L02:
       call      System.ThrowHelper.ThrowArrayTypeMismatchException()
       int       3
; Total bytes of code 66
```

and here’s what it shows for .NET 5:

```
; Program.Ctor()
       mov       rax,[rcx+8]
       test      rax,rax
       jne       short M00_L00
       xor       eax,eax
       jmp       short M00_L01
M00_L00:
       mov       eax,[rax+8]
M00_L01:
       ret
; Total bytes of code 17
```

As another example, in the GC discussion earlier I called out a bunch of benefits we’ve experienced from porting native runtime code to be managed C# code. One that I didn’t mention then but will now is that it’s resulted in us making other improvements in the system that addressed key blockers to such porting but that then also serve to improve many other cases. A good example of that is [dotnet/runtime#38229](https://github.com/dotnet/runtime/pull/38229). When we first moved the native array sorting implementation to managed, we inadvertently incurred a regression for floating-point values, a regression that was helpfully spotted by [@nietras](https://github.com/nietras) and which was subsequently fixed in [dotnet/runtime#37941](https://github.com/dotnet/runtime/pull/37941). The regression was due to the native implementation employing a special optimization that we were missing in the managed port (for floating-point arrays, moving all NaN values to the beginning of the array such that subsequent comparison operations could ignore the possibility of NaNs), and we successfully brought that over. The problem, however, was expressing this in a way that didn’t result in tons of code duplication: the native implementation used templates, and the managed implementation used generics, but a limitation in inlining with generics made it such that helpers introduced to avoid lots of code duplication were causing non-inlineable method calls on every comparison employed in the sort. PR [dotnet/runtime#38229](https://github.com/dotnet/runtime/pull/38229) addressed that by enabling the JIT to inline shared generic code within the same type. Consider this microbenchmark:

```
private C c1 = new C() { Value = 1 }, c2 = new C() { Value = 2 }, c3 = new C() { Value = 3 };

[Benchmark]
public int Compare() => Comparer<C>.Smallest(c1, c2, c3);

class Comparer<T> where T : IComparable<T>
{
    public static int Smallest(T t1, T t2, T t3) =>
        Compare(t1, t2) <= 0 ?
            (Compare(t1, t3) <= 0 ? 0 : 2) :
            (Compare(t2, t3) <= 0 ? 1 : 2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Compare(T t1, T t2) => t1.CompareTo(t2);
}

class C : IComparable<C>
{
    public int Value;
    public int CompareTo(C other) => other is null ? 1 : Value.CompareTo(other.Value);
}
```

The `Smallest` method is comparing the three supplied values and returning the index of the smallest. It is a method on a generic type, and it’s calling to another method on that same type, which is in turn making calls out to methods on an instance of the generic type parameter. As the benchmark is using `C` as the generic type, and as `C` is a reference type, the JIT will not specialize the code for this method specifically for `C`, and will instead use a “shared” implementation it generates to be used for all reference types. In order for the `Compare` method to then call out to the correct interface implementation of `CompareTo`, that shared generic implementation employs a dictionary that maps from the generic type to the right target. In previous versions of .NET, methods containing those generic dictionary lookups were not inlineable, which means that this `Smallest` method can’t inline the three calls it makes to `Compare`, even though `Compare` is attributed as `MethodImplOptions.AggressiveInlining`. The aforementioned PR removed that limitation, resulting in a very measurable speedup on this example (and making the array sorting regression fix feasible):

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Compare | .NET FW 4.8 | 8.632 ns | 1.00 |
| Compare | .NET Core 3.1 | 9.259 ns | 1.07 |
| Compare | .NET 5.0 | 5.282 ns | 0.61 |

Most of the cited improvements here have focused on throughput, with the JIT producing code that executes more quickly, and that faster code is often (though not always) smaller. Folks working on the JIT actually pay a lot of attention to code size, in many cases using it as a primary metric for whether a change is beneficial or not. Smaller code is not always faster code (instructions can be the same size but have very different cost profiles), but at a high level it’s a reasonable metric, and smaller code does have direct benefits, such as less impact on instruction caches, less code to load, etc. In some cases, changes are focused entirely on reducing code size, such as in cases where unnecessary duplication occurs. Consider this simple benchmark:

```
private int _offset = 0;

[Benchmark]
public int ThrowHelpers()
{
    var arr = new int[10];
    var s0 = new Span<int>(arr, _offset, 1);
    var s1 = new Span<int>(arr, _offset + 1, 1);
    var s2 = new Span<int>(arr, _offset + 2, 1);
    var s3 = new Span<int>(arr, _offset + 3, 1);
    var s4 = new Span<int>(arr, _offset + 4, 1);
    var s5 = new Span<int>(arr, _offset + 5, 1);
    return s0[0] + s1[0] + s2[0] + s3[0] + s4[0] + s5[0];
}
```

The `Span<T>` constructor does [argument validation](https://github.com/dotnet/runtime/blob/932098fe90d146a73ebd86a2e595398b63b1a600/src/libraries/System.Private.CoreLib/src/System/Span.cs#L68-L80), which, when `T` is a value type, results in there being two call sites to a method on the `ThrowHelper` class, one that throws for a failed null check on the input array and one that throws when offset and count are out of range (`ThrowHelper` contains non-inlinable methods like `ThrowArgumentNullException`, which contains the actual `throw` and avoids the associated code size at every call site; the JIT currently isn’t capable of “outlining”, the opposite of “inlining”, so it needs to be done manually in cases where it matters). In the above example, we’re creating six spans, which means six calls to the `Span<T>` constructor, all of which will be inlined. The JIT can see that the array is non-null, so it can eliminate the null check and the `ThrowArgumentNullException` from inlined code, but it doesn’t know whether the offset and count are in range, so it needs to retain the range check and the call site for the `ThrowHelper.ThrowArgumentOutOfRangeException` method. In .NET Core 3.1, that results in code like the following being generated for this `ThrowHelpers` method:

```
M00_L00:
       call      System.ThrowHelper.ThrowArgumentOutOfRangeException()
       int       3
M00_L01:
       call      System.ThrowHelper.ThrowArgumentOutOfRangeException()
       int       3
M00_L02:
       call      System.ThrowHelper.ThrowArgumentOutOfRangeException()
       int       3
M00_L03:
       call      System.ThrowHelper.ThrowArgumentOutOfRangeException()
       int       3
M00_L04:
       call      System.ThrowHelper.ThrowArgumentOutOfRangeException()
       int       3
M00_L05:
       call      System.ThrowHelper.ThrowArgumentOutOfRangeException()
       int       3
```

In .NET 5, thanks to [dotnet/coreclr#27113](https://github.com/dotnet/coreclr/pull/27113), the JIT is able to recognize this duplication, and instead of all six call sites, it’ll end up consolidating them into just one:

```
M00_L00:
       call      System.ThrowHelper.ThrowArgumentOutOfRangeException()
       int       3
```

with all failed checks jumping to this shared location rather than each having its own copy.

| Method | Runtime | Code Size |
| --- | --- | --- |
| ThrowHelpers | .NET FW 4.8 | 424 B |
| ThrowHelpers | .NET Core 3.1 | 252 B |
| ThrowHelpers | .NET 5.0 | 222 B |

These are just some of the myriad of improvements that have gone into the JIT in .NET 5. There are many more. [dotnet/runtime#32368](https://github.com/dotnet/runtime/pull/32368) causes the JIT to see an array’s length as unsigned, which results in it being able to use better instructions for some mathematical operations (e.g. division) performed on the length. [dotnet/coreclr#25458](https://github.com/dotnet/coreclr/pull/25458) enables the JIT to use faster 0-based comparisons for some unsigned integer operations, e.g. using the equivalent of `a != 0` when the developer actually wrote `a >= 1`. [dotnet/runtime#1378](https://github.com/dotnet/runtime/pull/1378) allows the JIT to recognize “constantString”.Length as a constant value. [dotnet/runtime#26740](https://github.com/dotnet/coreclr/pull/26740) reduces the size of ReadyToRun images by removing `nop` padding. [dotnet/runtime#330234](https://github.com/dotnet/runtime/pull/33024) optimizes the instructions generated when performing `x * 2` when `x` is a `float` or `double`, using an add instead of a multiply. [dotnet/runtime#27060](https://github.com/dotnet/coreclr/pull/27060) improves the code generated for the `Math.FusedMultiplyAdd` intrinsic. [dotnet/runtime#27384](https://github.com/dotnet/coreclr/pull/27384) makes volatile operations cheaper on ARM64 by using better fence instructions than were previously used, and [dotnet/runtime#38179](https://github.com/dotnet/runtime/pull/38179) performs a peephole optimization on ARM64 to remove a bunch of redundant `mov` instructions. And on and on.

There are also some significant changes in the JIT that are disabled by default, with the goal of getting real-world feedback on them and being able to enable them by default post-.NET 5. For example, [dotnet/runtime#32969](https://github.com/dotnet/runtime/pull/32969) provides an initial implementation of “On Stack Replacement” (OSR). I mentioned tiered compilation earlier, which enables the JIT to first generate minimally-optimized code for a method, and then subsequently recompile a method with much more optimization when that method is shown to be important. This enables faster start-up time by allowing code to get going more quickly and only upgrading impactful methods once things are running. However, tiered compilation relies on being able to replace an implementation, and the next time it’s called, the new one will be invoked. But what about long-running methods? Tiered compilation is disabled by default for methods that contain loops (or, more specifically, backward branches) because they could end up running for a long time such that the replacement may not be used in a timely manner. OSR enables methods to be updated while their code is executing, while they’re “on stack”; lots of great details are in the [design document](https://github.com/dotnet/runtime/blob/master/docs/design/features/OnStackReplacement.md) included in that PR (also related to tiered compilation, [dotnet/runtime#1457](https://github.com/dotnet/runtime/pull/1457) improves the call-counting mechanism by which tiered compilation decides which methods should be recompiled, and when). You can experiment with OSR by setting both the `COMPlus_TC_QuickJitForLoops` and `COMPlus_TC_OnStackReplacement` environment variables to `1`. As another example, [dotnet/runtime#1180](https://github.com/dotnet/runtime/pull/1180) improves the generated code quality for code inside try blocks, enabling the JIT to keep values in registers where it previously couldn’t. You can experiment with this by setting the `COMPlus_EnableEHWriteThr` environment variable to `1`.

There are also a bunch of pending pull requests to the JIT that haven’t yet been merged but that very well could be before .NET 5 is released (in addition to, I expect, many more that haven’t been put up yet but will before .NET 5 ships in a few months). For example, [dotnet/runtime#32716](https://github.com/dotnet/runtime/pull/32716) enables the JIT to replace some branching comparison like `a == 42 ? 3 : 2` with branchless implementations, which can help with performance when the hardware isn’t able to correctly predict which branch would be taken. Or [dotnet/runtime#37226](https://github.com/dotnet/runtime/pull/37226), which enables the JIT to take a pattern like `"hello"[0]` and replace it with just `h`; while generally a developer doesn’t write such code, this can help when inlining is involved, with a constant string passed into a method that gets inlined and that indexes into a constant location (generally after a length check, which, thanks to [dotnet/runtime#1378](https://github.com/dotnet/runtime/pull/1378), can also become a const). Or [dotnet/runtime#1224](https://github.com/dotnet/runtime/pull/1224), which improves the code generation for the `Bmi2.MultiplyNoFlags` intrinsic. Or [dotnet/runtime#37836](https://github.com/dotnet/runtime/pull/37836), which turns `BitOperations.PopCount` into an intrinsic in a manner that enables the JIT to recognize when it’s called with a constant argument and replace the whole operation with a precomputed constant. Or [dotnet/runtime#37254](https://github.com/dotnet/runtime/pull/37245), which removes null checks emitted when working with const strings. Or [dotnet/runtime#32000](https://github.com/dotnet/runtime/pull/32000) from [@damageboy](https://github.com/damageboy), which optimizes double negations.

### Intrinsics

In .NET Core 3.0, over a thousand new hardware intrinsics methods were added and recognized by the JIT to enable C# code to directly target instruction sets like SSE4 and AVX2 (see the [docs](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.x86)). These were then used to great benefit in a bunch of APIs in the core libraries. However, the intrinsics were limited to x86/x64 architectures. In .NET 5, a ton of effort has gone into adding thousands more, specific to ARM64, thanks to multiple contributors, and in particular [@TamarChristinaArm](https://github.com/TamarChristinaArm) from Arm Holdings. And as with their x86/x64 counterparts, these intrinsics have been put to good use inside core library functionality. For example, the `BitOperations.PopCount()` method was previously optimized to use the x86 POPCNT intrinsic, and for .NET 5, [dotnet/runtime#35636](https://github.com/dotnet/runtime/pull/35636) augments it to also be able to use the ARM VCNT or ARM64 CNT equivalent. Similarly, [dotnet/runtime#34486](https://github.com/dotnet/runtime/pull/34486) modified `BitOperations.LeadingZeroCount`, `TrailingZeroCount`, and `Log2` to utilize the corresponding instrincs. And at a higher level, [dotnet/runtime#33749](https://github.com/dotnet/runtime/pull/33749/) from [@Gnbrkm41](https://github.com/Gnbrkm41) augments multiple methods in `BitArray` to use ARM64 intrinsics to go along with the previously added support for SSE2 and AVX2. Lots of work has gone into ensuring that the `Vector` APIs perform well on ARM64, too, such as with [dotnet/runtime#37139](https://github.com/dotnet/runtime/pull/37139) and [dotnet/runtime#36156](https://github.com/dotnet/runtime/pull/36156).

Beyond ARM64, additional work has been done to vectorize more operations. For example, [@Gnbrkm41](https://github.com/Gnbrkm41) also submitted [dotnet/runtime#31993](https://github.com/dotnet/runtime/pull/31993), which utilized ROUNDPS/ROUNDPD on x64 and FRINPT/FRINTM on ARM64 to improve the code generated for the new `Vector.Ceiling` and `Vector.Floor` methods. And `BitOperations` (which is a relatively low-level type implemented for most operations as a 1:1 wrapper around the most appropriate hardware intrinsics) was not only improved in [dotnet/runtime#35650](https://github.com/dotnet/runtime/pull/35650) from [@saucecontrol](https://github.com/saucecontrol) but also had its usage in Corelib improved to be more efficient.

Finally, a whole slew of changes went into the JIT to better handle hardware intrinsics and vectorization in general, such as [dotnet/runtime#35421](https://github.com/dotnet/runtime/pull/35421), [dotnet/runtime#31834](https://github.com/dotnet/runtime/pull/31834), [dotnet/runtime#1280](https://github.com/dotnet/runtime/pull/1280), [dotnet/runtime#35857](https://github.com/dotnet/runtime/pull/35857), [dotnet/runtime#36267](https://github.com/dotnet/runtime/pull/36267), and [dotnet/runtime#35525](https://github.com/dotnet/runtime/pull/35525).

## Runtime Helpers

The GC and JIT represent large portions of the runtime, but there still remains significant portions of functionality in the runtime outside of these components, and those have similarly seen improvements.

It’s interesting to note that the JIT doesn’t generate code from scratch for everything. There are many places where pre-existing helper functions are invoked by the JIT, with the runtime supplying those helpers, and improvements to those helpers can have meaningful impact on programs. [dotnet/runtime#23548](https://github.com/dotnet/coreclr/pull/23548) is a great example. In libraries like `System.Linq`, we’ve shied away from adding additional type checks for covariant interfaces because of significantly higher overhead for them versus for normal interfaces. [dotnet/runtime#23548](https://github.com/dotnet/coreclr/pull/23548) (subsequently tweaked in [dotnet/runtime#34427](https://github.com/dotnet/runtime/pull/34427)) essentially adds a cache, such that the cost of these casts are amortized and end up being much faster overall. This is evident from a simple microbenchmark:

```
private List<string> _list = new List<string>();

// IReadOnlyCollection<out T> is covariant
[Benchmark] public bool IsIReadOnlyCollection() => IsIReadOnlyCollection(_list);
[MethodImpl(MethodImplOptions.NoInlining)]  private static bool IsIReadOnlyCollection(object o) => o is IReadOnlyCollection<int>;
```

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| IsIReadOnlyCollection | .NET FW 4.8 | 105.460 ns | 1.00 | 53 B |
| IsIReadOnlyCollection | .NET Core 3.1 | 56.252 ns | 0.53 | 59 B |
| IsIReadOnlyCollection | .NET 5.0 | 3.383 ns | 0.03 | 45 B |

Another set of impactful changes came in [dotnet/runtime#32270](https://github.com/dotnet/runtime/pull/32270) (with JIT support in [dotnet/runtime#31957](https://github.com/dotnet/runtime/pull/31957)). In the past, generic methods maintained just a few dedicated dictionary slots that could be used for fast lookup of the types associated with the generic method; once those slots were exhausted, it fell back to a slower lookup table. The need for this limitation no longer exists, and these changes enabled fast lookup slots to be used for all generic lookups.

```
[Benchmark]
public void GenericDictionaries()
{
    for (int i = 0; i < 14; i++)
        GenericMethod<string>(i);
}

[MethodImpl(MethodImplOptions.NoInlining)]
private static object GenericMethod<T>(int level)
{
    switch (level)
    {
        case 0: return typeof(T);
        case 1: return typeof(List<T>);
        case 2: return typeof(List<List<T>>);
        case 3: return typeof(List<List<List<T>>>);
        case 4: return typeof(List<List<List<List<T>>>>);
        case 5: return typeof(List<List<List<List<List<T>>>>>);
        case 6: return typeof(List<List<List<List<List<List<T>>>>>>);
        case 7: return typeof(List<List<List<List<List<List<List<T>>>>>>>);
        case 8: return typeof(List<List<List<List<List<List<List<List<T>>>>>>>>);
        case 9: return typeof(List<List<List<List<List<List<List<List<List<T>>>>>>>>>);
        case 10: return typeof(List<List<List<List<List<List<List<List<List<List<T>>>>>>>>>>);
        case 11: return typeof(List<List<List<List<List<List<List<List<List<List<List<T>>>>>>>>>>>);
        case 12: return typeof(List<List<List<List<List<List<List<List<List<List<List<List<T>>>>>>>>>>>>);
        default: return typeof(List<List<List<List<List<List<List<List<List<List<List<List<List<T>>>>>>>>>>>>>);
    }
}
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| GenericDictionaries | .NET FW 4.8 | 104.33 ns | 1.00 |
| GenericDictionaries | .NET Core 3.1 | 76.71 ns | 0.74 |
| GenericDictionaries | .NET 5.0 | 51.53 ns | 0.49 |

## Text Processing

Text-based processing is the bread-and-butter of many applications, and a lot of effort in every release goes into improving the fundamental building blocks on top of which everything else is built. Such changes extend from microoptimizations in helpers processing individual characters all the way up to overhauls of entire text-processing libraries.

`System.Char` received some nice improvements in .NET 5. For example, [dotnet/coreclr#26848](https://github.com/dotnet/coreclr/pull/26848) improved the performance of `char.IsWhiteSpace` by tweaking the implementation to require fewer instructions and less branching. Improvements to `char.IsWhiteSpace` then manifest in a bunch of other methods that rely on it, like `string.IsEmptyOrWhiteSpace` and `Trim`:

```
[Benchmark]
public int Trim() => " test ".AsSpan().Trim().Length;
```

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| Trim | .NET FW 4.8 | 21.694 ns | 1.00 | 569 B |
| Trim | .NET Core 3.1 | 8.079 ns | 0.37 | 377 B |
| Trim | .NET 5.0 | 6.556 ns | 0.30 | 365 B |

Another nice example, [dotnet/runtime#35194](https://github.com/dotnet/runtime/pull/35194) improved the performance of `char.ToUpperInvariant` and `char.ToLowerInvariant` by improving the inlineability of various methods, streamlining the call paths from the public APIs down to the core functionality, and further tweaking the implementation to ensure the JIT was generating the best code.

```
[Benchmark]
[Arguments("It's exciting to see great performance!")]
public int ToUpperInvariant(string s)
{
    int sum = 0;

    for (int i = 0; i < s.Length; i++)
        sum += char.ToUpperInvariant(s[i]);

    return sum;
}
```

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| ToUpperInvariant | .NET FW 4.8 | 208.34 ns | 1.00 | 171 B |
| ToUpperInvariant | .NET Core 3.1 | 166.10 ns | 0.80 | 164 B |
| ToUpperInvariant | .NET 5.0 | 69.15 ns | 0.33 | 105 B |

Going beyond single characters, in practically every release of .NET Core, we’ve worked to push the envelope for how fast we can make the existing formatting APIs. This release is no different. And even though previous releases saw significant wins, this one moves the bar further.

`Int32.ToString()` is an incredibly common operation, and it’s important it be fast. [dotnet/runtime#32528](https://github.com/dotnet/runtime/pull/32528) from [@ts2do](https://github.com/ts2do) made it even faster by adding inlineable fast paths for the key formatting routines employed by the method and by streamlining the path taken by various public APIs to get to those routines. Other primitive `ToString` operations were also improved. For example, [dotnet/runtime#27056](https://github.com/dotnet/coreclr/pull/27056) streamlines some code paths to enable less cruft in getting from the public API to the point where bits are actually written out to memory.

```
[Benchmark] public string ToString12345() => 12345.ToString();
[Benchmark] public string ToString123() => ((byte)123).ToString();
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| ToString12345 | .NET FW 4.8 | 45.737 ns | 1.00 | 40 B |
| ToString12345 | .NET Core 3.1 | 20.006 ns | 0.44 | 32 B |
| ToString12345 | .NET 5.0 | 10.742 ns | 0.23 | 32 B |
|  |  |  |  |  |
| ToString123 | .NET FW 4.8 | 42.791 ns | 1.00 | 32 B |
| ToString123 | .NET Core 3.1 | 18.014 ns | 0.42 | 32 B |
| ToString123 | .NET 5.0 | 7.801 ns | 0.18 | 32 B |

In a similar vein, in previous releases we did some fairly heavy optimizations on `DateTime` and `DateTimeOffset`, but those improvements were primarily focused on how quickly we could convert the day/month/year/etc. data into the right characters or bytes and write them to the destination. In [dotnet/runtime#1944](https://github.com/dotnet/runtime/pull/1944), [@ts2do](https://github.com/ts2do) focused on the step before that, optimizing the extraction of the day/month/year/etc. from the raw tick count the `DateTime{Offset}` stores. That ended up being very fruitful, resulting in being able to output formats like “o” (the “round-trip date/time pattern”) 30% faster than before (the change also applied the same decomposition optimization in other places in the codebase where those components were needed from a `DateTime`, but the improvement is easiest to show in a benchmark for formatting):

```
private byte[] _bytes = new byte[100];
private char[] _chars = new char[100];
private DateTime _dt = DateTime.Now;

[Benchmark] public bool FormatChars() => _dt.TryFormat(_chars, out _, "o");
[Benchmark] public bool FormatBytes() => Utf8Formatter.TryFormat(_dt, _bytes, out _, 'O');
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| FormatChars | .NET Core 3.1 | 242.4 ns | 1.00 |
| FormatChars | .NET 5.0 | 176.4 ns | 0.73 |
|  |  |  |  |
| FormatBytes | .NET Core 3.1 | 235.6 ns | 1.00 |
| FormatBytes | .NET 5.0 | 176.1 ns | 0.75 |

There were also a multitude of improvements for operations on `strings`, such as with [dotnet/coreclr#26621](https://github.com/dotnet/coreclr/pull/26621) and [dotnet/coreclr#26962](https://github.com/dotnet/coreclr/pull/26962), which in some cases significantly improved the performance of culture-aware `StartsWith` and `EndsWith` operations on Linux.

Of course, low-level processing is all well and good, but applications these days spend a lot of time doing higher-level operations like encoding of data in a particular format, such as UTF8. Previous .NET Core releases saw `Encoding.UTF8` optimized, but in .NET 5 it’s still improved further. [dotnet/runtime#27268](https://github.com/dotnet/coreclr/pull/27268) optimizes it more, in particular for smaller inputs, by taking better advantage of stack allocation and improvements made in JIT devirtualization (where the JIT is able to avoid virtual dispatch due to being able to discover the actual concrete type of the instance it’s working with).

```
[Benchmark]
public string Roundtrip()
{
    byte[] bytes = Encoding.UTF8.GetBytes("this is a test");
    return Encoding.UTF8.GetString(bytes);
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Roundtrip | .NET FW 4.8 | 113.69 ns | 1.00 | 96 B |
| Roundtrip | .NET Core 3.1 | 49.76 ns | 0.44 | 96 B |
| Roundtrip | .NET 5.0 | 36.70 ns | 0.32 | 96 B |

As important as UTF8 is, the “ISO-8859-1” encoding, otherwise known as “Latin1” (and which is now publicly exposed as `Encoding.Latin1` via [dotnet/runtime#37550](https://github.com/dotnet/runtime/pull/37550)), is also very important, in particular for networking protocols like HTTP. [dotnet/runtime#32994](https://github.com/dotnet/runtime/pull/32994) vectorized its implementation, based in large part on similar optimizations previously done for `Encoding.ASCII`. This yields a really nice performance boost, which can measurably impact higher-level usage in clients like `HttpClient` and in servers like Kestrel.

```
private static readonly Encoding s_latin1 = Encoding.GetEncoding("iso-8859-1");

[Benchmark]
public string Roundtrip()
{
    byte[] bytes = s_latin1.GetBytes("this is a test. this is only a test. did it work?");
    return s_latin1.GetString(bytes);
}
```

| Method | Runtime | Mean | Allocated |
| --- | --- | --- | --- |
| Roundtrip | .NET FW 4.8 | 221.85 ns | 209 B |
| Roundtrip | .NET Core 3.1 | 193.20 ns | 200 B |
| Roundtrip | .NET 5.0 | 41.76 ns | 200 B |

Performance improvements to encoding also expanded to the encoders in `System.Text.Encodings.Web`, where PRs [dotnet/corefx#42073](https://github.com/dotnet/corefx/pull/42073) and [dotnet/runtime#284](https://github.com/dotnet/runtime/pull/284) from [@gfoidl](https://github.com/gfoidl) improved the various `TextEncoder` types. This included using SSSE3 instructions to vectorize `FindFirstCharacterToEncodeUtf8` as well as `FindFirstCharToEncode` in the `JavaScriptEncoder.Default` implementation.

```
private char[] _dest = new char[1000];

[Benchmark]
public void Encode() => JavaScriptEncoder.Default.Encode("This is a test to see how fast we can encode something that does not actually need encoding", _dest, out _, out _);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Encode | .NET Core 3.1 | 102.52 ns | 1.00 |
| Encode | .NET 5.0 | 33.39 ns | 0.33 |

### Regular Expressions

A very specific but extremely common form of parsing is via regular expressions. Back in early April, I shared a [detailed blog post](https://devblogs.microsoft.com/dotnet/regex-performance-improvements-in-net-5/) about some of the myriad of performance improvements that have gone into .NET 5 for System.Text.RegularExpressions. I’m not going to rehash all of that here, but I would encourage you to read it if haven’t already, as it represents significant advancements in the library. However, I also noted in that post that we would continue to improve `Regex`, and we have, in particular adding in more support for special but common cases.

One such improvement was in newline handling when specifying `RegexOptions.Multiline`, which changes the meaning of the `^` and `$` anchors to match at the beginning and end of any line rather than just the beginning and end of the whole input string. We previously didn’t do any special handling of beginning-of-line anchors (`^` when `Multiline` is specified), which meant that as part of the `FindFirstChar` operation (see the aforementioned blog post for background on what that refers to), we wouldn’t skip ahead as much as we otherwise could. [dotnet/runtime#34566](https://github.com/dotnet/runtime/pull/34566) taught `FindFirstChar` how to use a vectorized `IndexOf` to jump ahead to the next relevant location. The impact of that is highlighted in this benchmark, which is processing the text of “Romeo and Juliet” as downloaded from [Project Gutenberg](http://www.gutenberg.org/cache/epub/1112/pg1112.txt):

```
private readonly string _input = new HttpClient().GetStringAsync("http://www.gutenberg.org/cache/epub/1112/pg1112.txt").Result;
private Regex _regex;

[Params(false, true)]
public bool Compiled { get; set; }

[GlobalSetup]
public void Setup() => _regex = new Regex(@"^.*\blove\b.*$", RegexOptions.Multiline | (Compiled ? RegexOptions.Compiled : RegexOptions.None));

[Benchmark]
public int Count() => _regex.Matches(_input).Count;
```

| Method | Runtime | Compiled | Mean | Ratio |
| --- | --- | --- | --- | --- |
| Count | .NET FW 4.8 | False | 26.207 ms | 1.00 |
| Count | .NET Core 3.1 | False | 21.106 ms | 0.80 |
| Count | .NET 5.0 | False | 4.065 ms | 0.16 |
|  |  |  |  |  |
| Count | .NET FW 4.8 | True | 16.944 ms | 1.00 |
| Count | .NET Core 3.1 | True | 15.287 ms | 0.90 |
| Count | .NET 5.0 | True | 2.172 ms | 0.13 |

Another such improvement was in the handling of `RegexOptions.IgnoreCase`. The implementation of `IgnoreCase` uses `char.ToLower{Invariant}` to get the relevant characters to be compared, but that has overhead due to culture-specific mappings. [dotnet/runtime#35185](https://github.com/dotnet/runtime/pull/35185) enables those overheads to be avoided when the only character that could possibly lowercase to the character being compared against is that character itself.

```
private readonly Regex _regex = new Regex("hello.*world", RegexOptions.Compiled | RegexOptions.IgnoreCase);
private readonly string _input = "abcdHELLO" + new string('a', 128) + "WORLD123";

[Benchmark] public bool IsMatch() => _regex.IsMatch(_input);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| IsMatch | .NET FW 4.8 | 2,558.1 ns | 1.00 |
| IsMatch | .NET Core 3.1 | 789.3 ns | 0.31 |
| IsMatch | .NET 5.0 | 129.0 ns | 0.05 |

Related to that improvement is [dotnet/runtime#35203](https://github.com/dotnet/runtime/pull/35203), which, also in service of `RegexOptions.IgnoreCase`, reduces the number of virtual calls the implementation was making to `CultureInfo.TextInfo`, caching the `TextInfo` instead of the `CultureInfo` from which it came.

```
private readonly Regex _regex = new Regex("Hello, \\w+.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
private readonly string _input = "This is a test to see how well this does.  Hello, world.";

[Benchmark] public bool IsMatch() => _regex.IsMatch(_input);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| IsMatch | .NET FW 4.8 | 712.9 ns | 1.00 |
| IsMatch | .NET Core 3.1 | 343.5 ns | 0.48 |
| IsMatch | .NET 5.0 | 100.9 ns | 0.14 |

One of my favorite recent optimizations, though, was [dotnet/runtime#35824](https://github.com/dotnet/runtime/pull/35824) (which was then augmented further in [dotnet/runtime#35936](https://github.com/dotnet/runtime/pull/35936)). The change recognizes that, for a regex beginning with an atomic loop (one explicitly written or more commonly one upgraded to being atomic by automatic analysis of the expression), we can update the next starting position in the scan loop (again, see the blog post for details) based on where the loop ended rather than on where it started. For many inputs, this can provide a big reduction in overhead. Using the benchmark and data from [https://github.com/mariomka/regex-benchmark](https://github.com/mariomka/regex-benchmark):

```
private Regex _email = new Regex(@"[\w\.+-]+@[\w\.-]+\.[\w\.-]+", RegexOptions.Compiled);
private Regex _uri = new Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.Compiled);
private Regex _ip = new Regex(@"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])", RegexOptions.Compiled);

private string _input = new HttpClient().GetStringAsync("https://raw.githubusercontent.com/mariomka/regex-benchmark/652d55810691ad88e1c2292a2646d301d3928903/input-text.txt").Result;

[Benchmark] public int Email() => _email.Matches(_input).Count;
[Benchmark] public int Uri() => _uri.Matches(_input).Count;
[Benchmark] public int IP() => _ip.Matches(_input).Count;
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Email | .NET FW 4.8 | 1,036.729 ms | 1.00 |
| Email | .NET Core 3.1 | 930.238 ms | 0.90 |
| Email | .NET 5.0 | 50.911 ms | 0.05 |
|  |  |  |  |
| Uri | .NET FW 4.8 | 870.114 ms | 1.00 |
| Uri | .NET Core 3.1 | 759.079 ms | 0.87 |
| Uri | .NET 5.0 | 50.022 ms | 0.06 |
|  |  |  |  |
| IP | .NET FW 4.8 | 75.718 ms | 1.00 |
| IP | .NET Core 3.1 | 61.818 ms | 0.82 |
| IP | .NET 5.0 | 6.837 ms | 0.09 |

Finally, not all focus was on the raw throughput of actually executing regular expressions. One of the ways developers can get the best throughput with `Regex` is by specifying `RegexOptions.Compiled`, which uses Reflection Emit to at runtime generate IL, which in turn needs to be JIT compiled. Depending on the expressions employed, `Regex` may spit out a fair amount of IL, which then can require a non-trivial amount of JIT processing to churn into assembly code. [dotnet/runtime#35352](https://github.com/dotnet/runtime/pull/35352) improved the JIT itself to help with this case, fixing some potentially quadratic-execution-time code paths the regex-generated IL was triggering. And [dotnet/runtime#35321](https://github.com/dotnet/runtime/pull/35321) tweaked the IL operations used by `Regex` engine to employ patterns much closer to what the C# compiler would emit, which is important because those same patterns are what the JIT is more tuned to optimize well. On some real-world workloads featuring several hundred complex regular expressions, these combined to reduce the time it took to JIT the expressions by upwards of 20%.

## Threading and Async

One of the biggest changes around asynchrony in .NET 5 is actually not enabled by default, but is another experiment to get feedback. The [Async ValueTask Pooling in .NET 5](https://devblogs.microsoft.com/dotnet/async-valuetask-pooling-in-net-5/) blog post explains this in much more detail, but essentially [dotnet/coreclr#26310](https://github.com/dotnet/coreclr/pull/26310) introduced the ability for `async ValueTask` and `async ValueTask<T>` to implicitly cache and reuse the object created to represent an asynchronously completing operation, making the overhead of such methods amortized-allocation-free. The optimization is currently opt-in, meaning you need to set the `DOTNET_SYSTEM_THREADING_POOLASYNCVALUETASKS` environment variable to `1` in order to enable it. One of the difficulties with enabling this is for code that might be doing something more complex than just `await SomeValueTaskReturningMethod()`, as `ValueTasks` have more constraints than `Task`s about how they can be used. To help with that, a new [`UseValueTasksCorrectly` analyzer](https://docs.microsoft.com/en-us/visualstudio/code-quality/ca2012) was released that will flag most such misuse.

```
[Benchmark]
public async Task ValueTaskCost()
{
    for (int i = 0; i < 1_000; i++)
        await YieldOnce();
}

private static async ValueTask YieldOnce() => await Task.Yield();
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| ValueTaskCost | .NET FW 4.8 | 1,635.6 us | 1.00 | 294010 B |
| ValueTaskCost | .NET Core 3.1 | 842.7 us | 0.51 | 120184 B |
| ValueTaskCost | .NET 5.0 | 812.3 us | 0.50 | 186 B |

Some changes in the C# compiler accrue additional benefits to async methods in .NET 5 (in that the core libraries in .NET 5 are compiled with the newer compiler). Every async method has a “builder” that’s responsible for producing and completing the returned task, with the C# compiler generating code as part of an async method to use one. [dotnet/roslyn#41253](https://github.com/dotnet/roslyn/pull/41253) from [@benaadams](https://github.com/benaadams) avoids a struct copy generated as part of that code, which can help reduce overheads, in particular for `async ValueTask<T>` methods where the builder is relatively large (and grows as `T` grows). [dotnet/roslyn#45262](https://github.com/dotnet/roslyn/pull/45262) also from [@benaadams](https://github.com/benaadams) also tweaks the same generated code to play better with the JIT’s zero’ing improvements discussed previously.

There are also some improvements in specific APIs. [dotnet/runtime#35575](https://github.com/dotnet/runtime/pull/35575) was born out of some specific usage of `Task.ContinueWith`, where a continuation is used purely for the purposes of logging an exception in the “antecedent” `Task` continued from. The common case here is that the `Task` doesn’t fault, and this PR does a better job optimizing for that case.

```
const int Iters = 1_000_000;

private AsyncTaskMethodBuilder[] tasks = new AsyncTaskMethodBuilder[Iters];

[IterationSetup]
public void Setup()
{
    Array.Clear(tasks, 0, tasks.Length);
    for (int i = 0; i < tasks.Length; i++)
        _ = tasks[i].Task;
}

[Benchmark(OperationsPerInvoke = Iters)]
public void Cancel()
{
    for (int i = 0; i < tasks.Length; i++)
    {
        tasks[i].Task.ContinueWith(_ => { }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        tasks[i].SetResult();
    }
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Cancel | .NET FW 4.8 | 239.2 ns | 1.00 | 193 B |
| Cancel | .NET Core 3.1 | 140.3 ns | 0.59 | 192 B |
| Cancel | .NET 5.0 | 106.4 ns | 0.44 | 112 B |

There were also tweaks to help with specific architectures. Because of the strong memory model employed by x86/x64 architectures, `volatile` essentially evaporates at JIT time when targeting x86/x64. That is not the case for ARM/ARM64, which have weaker memory models and where `volatile` results in fences being emitted by the JIT. [dotnet/runtime#36697](https://github.com/dotnet/runtime/pull/36697) removes several volatile accesses per work item queued to the `ThreadPool`, making the `ThreadPool` faster on ARM. [dotnet/runtime#34225](https://github.com/dotnet/runtime/pull/34225) hoisted a volatile access in `ConcurrentDictionary` out of a loop, which in turn improved throughput of some members on `ConcurrentDictionary` on ARM by as much as 30%. And [dotnet/runtime#36976](https://github.com/dotnet/runtime/pull/36976) removed `volatile` entirely from another `ConcurrentDictionary` field.

## Collections

Over the years, C# has gained a plethora of valuable features. Many of these features are focused on developers being able to more succinctly write code, with the language/compiler being responsible for all the boilerplate, such as with [records in C# 9](https://devblogs.microsoft.com/dotnet/welcome-to-c-9-0/). However, a few features are focused less on productivity and more on performance, and such features are a great boon to the core libraries, which can often use them to make everyone’s program’s more efficient. [dotnet/runtime#27195](https://github.com/dotnet/coreclr/pull/27195) from [@benaadams](https://github.com/benaadams) is a good example of this. The PR improves `Dictionary<TKey, TValue>`, taking advantage of ref returns and ref locals, which were introduced in C# 7. `Dictionary<TKey, TValue>`‘s implementation is backed by an array of entries in the dictionary, and the dictionary has a core routine for looking up a key’s index in its entries array; that routine is then used from multiple functions, like the indexer, `TryGetValue`, `ContainsKey`, and so on. However, that sharing comes at a cost: by handing back the index and leaving it up to the caller to get the data from that slot as needed, the caller would need to re-index into the array, incurring a second bounds check. With ref returns, that shared routine could instead hand back a ref to the slot rather than the raw index, enabling the caller to avoid the second bounds check while also avoiding making a copy of the entire entry. The PR also included some low-level tuning of the generated assembly, reorganizing fields and the operations used to update those fields in a way that enabled the JIT to better tune the generated assembly.

`Dictionary<TKey,TValue>`‘s performance was improved further by several more PRs. Like many hash tables, `Dictionary<TKey,TValue>` is partitioned into “buckets”, each of which is essentially a linked list of entries (stored in an array, not with individual node objects per item). For a given key, a hashing function (`TKey`‘s `GetHashCode` or the supplied `IComparer<T>`‘s `GetHashCode`) is used to compute a hash code for the supplied key, and then that hash code is mapped deterministically to a bucket; once the bucket is found, the implementation then iterates through the chain of entries in that bucket looking for the target key. The implementation tries to keep the number of entries in each bucket small, growing and rebalancing as necessary to maintain that condition. As such, a large portion of the cost of a lookup is computing the hashcode-to-bucket mapping. In order to help maintain a good distribution across the buckets, especially when a less-than-ideal hash code generator is employed by the supplied `TKey` or comparer, the dictionary uses a prime number of buckets, and the bucket mapping is done by `hashcode % numBuckets`. But at the speeds important here, the division employed by the `%` operator is relatively expensive. Building on [Daniel Lemire’s work](https://lemire.me/blog/2019/02/08/faster-remainders-when-the-divisor-is-a-constant-beating-compilers-and-libdivide/), [dotnet/coreclr#27299](https://github.com/dotnet/coreclr/pull/27299) from [@benaadams](https://github.com/benaadams) and then [dotnet/runtime#406](https://github.com/dotnet/runtime/pull/406) changed the use of `%` in 64-bit processes to instead use a couple of multiplications and shifts to achieve the same result but faster.

```
private Dictionary<int, int> _dictionary = Enumerable.Range(0, 10_000).ToDictionary(i => i);

[Benchmark]
public int Sum()
{
    Dictionary<int, int> dictionary = _dictionary;
    int sum = 0;

    for (int i = 0; i < 10_000; i++)
        if (dictionary.TryGetValue(i, out int value))
            sum += value;

    return sum;
}
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Sum | .NET FW 4.8 | 77.45 us | 1.00 |
| Sum | .NET Core 3.1 | 67.35 us | 0.87 |
| Sum | .NET 5.0 | 44.10 us | 0.57 |

`HashSet<T>` is very similar to `Dictionary<TKey, TValue>`. While it exposes a different set of operations (no pun intended), other than only storing a key rather than a key and a value, its data structure is fundamentally the same… or, at least, it used to be. Over the years, given how much more `Dictionary<TKey,TValue>` is used than `HashSet<T>`, more effort has gone into optimizing `Dictionary<TKey, TValue>`‘s implementation, and the two implementations have drifted. [dotnet/corefx#40106](https://github.com/dotnet/corefx/pull/40106) from [@JeffreyZhao](https://github.com/JeffreyZhao) ported some of the improvements from dictionary to hash set, and then [dotnet/runtime#37180](https://github.com/dotnet/runtime/pull/37180) effectively rewrote `HashSet<T>`‘s implementation by re-syncing it with dictionary’s (along with moving it lower in the stack so that some places a dictionary was being used for a set could be properly replaced). The net result is that `HashSet<T>` ends up experiencing similar gains (more so even, because it was starting from a worse place).

```
private HashSet<int> _set = Enumerable.Range(0, 10_000).ToHashSet();

[Benchmark]
public int Sum()
{
    HashSet<int> set = _set;
    int sum = 0;

    for (int i = 0; i < 10_000; i++)
        if (set.Contains(i))
            sum += i;

    return sum;
}
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Sum | .NET FW 4.8 | 76.29 us | 1.00 |
| Sum | .NET Core 3.1 | 79.23 us | 1.04 |
| Sum | .NET 5.0 | 42.63 us | 0.56 |

Similarly, [dotnet/runtime#37081](https://github.com/dotnet/runtime/pull/37081) ported similar improvements from `Dictionary<TKey, TValue>` to `ConcurrentDictionary<TKey, TValue>`.

```
private ConcurrentDictionary<int, int> _dictionary = new ConcurrentDictionary<int, int>(Enumerable.Range(0, 10_000).Select(i => new KeyValuePair<int, int>(i, i)));

[Benchmark]
public int Sum()
{
    ConcurrentDictionary<int, int> dictionary = _dictionary;
    int sum = 0;

    for (int i = 0; i < 10_000; i++)
        if (dictionary.TryGetValue(i, out int value))
            sum += value;

    return sum;
}
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Sum | .NET FW 4.8 | 115.25 us | 1.00 |
| Sum | .NET Core 3.1 | 84.30 us | 0.73 |
| Sum | .NET 5.0 | 49.52 us | 0.43 |

System.Collections.Immutable has also seen improvements in the release. [dotnet/runtime#1183](https://github.com/dotnet/runtime/pull/1183) is a one-line but impactful change from [@hnrqbaggio](https://github.com/hnrqbaggio) to improve the performance of `foreach`‘ing over an `ImmutableArray<T>` by adding `[MethodImpl(MethodImplOptions.AggressiveInlining)]` to `ImmutableArray<T>`‘s `GetEnumerator` method. We’re generally very cautious about sprinkling `AggressiveInlining` around: it can make microbenchmarks look really good, since it ends up eliminating the overhead of calling the relevant method, but it can also significantly increase code size, which can then negatively impact a whole bunch of things, such as causing the instruction cache to become much less effective. In this case, however, it not only improves throughput but also actually reduces code size. Inlining is a powerful optimization, not just because it eliminates the overhead of a call, but because it exposes the contents of the callee to the caller. The JIT generally doesn’t do interprocedural analysis, due to the JIT’s limited time budget for optimizations, but inlining overcomes that by merging the caller and the callee, at which point the JIT optimizations of the caller factor in the callee. Imagine a method `public static int GetValue() => 42;` and a caller that does `if (GetValue() * 2 > 100) { ... lots of code ... }`. If `GetValue()` isn’t inlined, that comparison and “lots of code” will get JIT’d, but if `GetValue()` is inlined, the JIT will see this as `if (84 > 100) { ... lots of code ... }`, and the whole block will be dropped. Thankfully such a simple method will almost always be automatically inlined, but `ImmutableArray<T>`‘s `GetEnumerator` is just large enough that the JIT doesn’t recognize automatically how beneficial it will be. In practice, when the `GetEnumerator` is inlined, the JIT ends up being able to better recognize that the `foreach` is iterating over an array, and instead of the generated code for `Sum` being:

```
; Program.Sum()
       push      rsi
       sub       rsp,30
       xor       eax,eax
       mov       [rsp+20],rax
       mov       [rsp+28],rax
       xor       esi,esi
       cmp       [rcx],ecx
       add       rcx,8
       lea       rdx,[rsp+20]
       call      System.Collections.Immutable.ImmutableArray'1[[System.Int32, System.Private.CoreLib]].GetEnumerator()
       jmp       short M00_L01
M00_L00:
       cmp       [rsp+28],edx
       jae       short M00_L02
       mov       rax,[rsp+20]
       mov       edx,[rsp+28]
       movsxd    rdx,edx
       mov       eax,[rax+rdx*4+10]
       add       esi,eax
M00_L01:
       mov       eax,[rsp+28]
       inc       eax
       mov       [rsp+28],eax
       mov       rdx,[rsp+20]
       mov       edx,[rdx+8]
       cmp       edx,eax
       jg        short M00_L00
       mov       eax,esi
       add       rsp,30
       pop       rsi
       ret
M00_L02:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 97
```

as it is in .NET Core 3.1, in .NET 5 it ends up being

```
; Program.Sum()
       sub       rsp,28
       xor       eax,eax
       add       rcx,8
       mov       rdx,[rcx]
       mov       ecx,[rdx+8]
       mov       r8d,0FFFFFFFF
       jmp       short M00_L01
M00_L00:
       cmp       r8d,ecx
       jae       short M00_L02
       movsxd    r9,r8d
       mov       r9d,[rdx+r9*4+10]
       add       eax,r9d
M00_L01:
       inc       r8d
       cmp       ecx,r8d
       jg        short M00_L00
       add       rsp,28
       ret
M00_L02:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 59
```

So, much smaller code and much faster execution:

```
private ImmutableArray<int> _array = ImmutableArray.Create(Enumerable.Range(0, 100_000).ToArray());

[Benchmark]
public int Sum()
{
    int sum = 0;

    foreach (int i in _array)
        sum += i;

    return sum;
}
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Sum | .NET FW 4.8 | 187.60 us | 1.00 |
| Sum | .NET Core 3.1 | 187.32 us | 1.00 |
| Sum | .NET 5.0 | 46.59 us | 0.25 |

`ImmutableList<T>.Contains` also saw significant improvements due to [dotnet/corefx#40540](https://github.com/dotnet/corefx/pull/40540) from [@shortspider](https://github.com/shortspider). `Contains` had been implemented using `ImmutableList<T>`‘s `IndexOf` method, which is in turn implemented on top of its `Enumerator`. Under the covers `ImmutableList<T>` is implemented today as an [AVL tree](https://en.wikipedia.org/wiki/AVL_tree), a form of self-balancing binary search tree, and in order to walk such a tree in order, it needs to retain a non-trivial amount of state, and `ImmutableList<T>`‘s enumerator goes to great pains to avoid allocating per enumeration in order to store that state. That results in non-trivial overhead. However, `Contains` doesn’t care about the exact index of an element in the list (nor which of potentially multiple copies is found), just that it’s there, and as such, it can employ a trivial recursive tree search. (And because the tree is balanced, we’re not concerned about stack overflow conditions.)

```
private ImmutableList<int> _list = ImmutableList.Create(Enumerable.Range(0, 1_000).ToArray());

[Benchmark]
public int Sum()
{
    int sum = 0;

    for (int i = 0; i < 1_000; i++)
        if (_list.Contains(i))
            sum += i;

    return sum;
}
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Sum | .NET FW 4.8 | 22.259 ms | 1.00 |
| Sum | .NET Core 3.1 | 22.872 ms | 1.03 |
| Sum | .NET 5.0 | 2.066 ms | 0.09 |

The previously highlighted collection improvements were all to general-purpose collections, meant to be used with whatever data the developer needs stored. But not all collection types are like that: some are much more specialized to a particular data type, and such collections see performance improvements in .NET 5 as well. `BitArray` is one such example, with several PRs this release making significant improvements to its performance. In particular, [dotnet/corefx#41896](https://github.com/dotnet/corefx/pull/41896) from [@Gnbrkm41](https://github.com/Gnbrkm41) utilized AVX2 and SSE2 intrinsics to vectorize many of the operations on `BitArray` ([dotnet/runtime#33749](https://github.com/dotnet/runtime/pull/33749) subsequently added ARM64 intrinsics, as well):

```
private bool[] _array;

[GlobalSetup]
public void Setup()
{
    var r = new Random(42);
    _array = Enumerable.Range(0, 1000).Select(_ => r.Next(0, 2) == 0).ToArray();
}

[Benchmark]
public BitArray Create() => new BitArray(_array);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Create | .NET FW 4.8 | 1,140.91 ns | 1.00 |
| Create | .NET Core 3.1 | 861.97 ns | 0.76 |
| Create | .NET 5.0 | 49.08 ns | 0.04 |

### LINQ

Previous releases of .NET Core saw a large amount of churn in the `System.Linq` codebase, in particular to improve performance. That flow has slowed, but .NET 5 still sees performance improvements in LINQ.

One noteable improvement is in `OrderBy`. As discussed earlier, there were multiple motivations for moving coreclr’s native sorting implementation up into managed code, one of which was being able to reuse it easily as part of span-based sorting methods. Such APIs were exposed publicly, and with [dotnet/runtime#1888](https://github.com/dotnet/runtime/pull/1888#issuecomment-575861604), we were able to utilize that span-based sorting in `System.Linq`. This was beneficial in particular because it enabled utilizing the `Comparison<T>`\-based sorting routines, which in turn enabled avoiding multiple levels of indirection on every comparison operation.

```
[GlobalSetup]
public void Setup()
{
    var r = new Random(42);
    _array = Enumerable.Range(0, 1_000).Select(_ => r.Next()).ToArray();
}

private int[] _array;

[Benchmark]
public void Sort()
{
    foreach (int i in _array.OrderBy(i => i)) { }
}
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Sort | .NET FW 4.8 | 100.78 us | 1.00 |
| Sort | .NET Core 3.1 | 101.03 us | 1.00 |
| Sort | .NET 5.0 | 85.46 us | 0.85 |

Not bad for a one-line change.

Another improvement was [dotnet/corefx#41342](https://github.com/dotnet/corefx/pull/41342) from [@timandy](https://github.com/timandy). The PR augmented `Enumerable.SkipLast` to special-case `IList<T>` as well as the internal `IPartition<T>` interface (which is how various operators communicate with each other for optimization purposes) in order to re-express `SkipLast` as a `Take` operation when the length of the source could be cheaply determined.

```
private IEnumerable<int> data = Enumerable.Range(0, 100).ToList();

[Benchmark]
public int SkipLast() => data.SkipLast(5).Sum();
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| SkipLast | .NET Core 3.1 | 1,641.0 ns | 1.00 | 248 B |
| SkipLast | .NET 5.0 | 684.8 ns | 0.42 | 48 B |

As a final example, [dotnet/corefx#40377](https://github.com/dotnet/corefx/pull/40377) was arguably a long time coming. This is an interesting case to me. For a while now I’ve seen developers assume that `Enumerable.Any()` is more efficient than `Enumerable.Count() != 0`; after all, `Any()` only needs to determine whether there’s anything in the source, and `Count()` needs to determine how many things there are in the source. Thus, with any reasonable collection, `Any()` should at worst case be O(1) and `Count()` may at worst case be O(N), so wouldn’t `Any()` always be preferable? There are even Roslyn analyzers that recommend this conversion. Unfortunately, it’s not always the case. Until .NET 5, `Any()` was implemented essentially as follows:

```
using (IEnumerator<T> e = source.GetEnumerator)
    return e.MoveNext();
```

That means that in the common case, even though it’s likely an O(1) operation, it’s going to result in an enumerator object being allocated as well as two interface dispatches. In contrast, since the initial release of LINQ in .NET Framework 3.0, `Count()` has had optimized code paths that special-case `ICollection<T>` to use its `Count` property, in which case generally it’s going to be O(1) and allocation-free with only one interface dispatch. As a result, for very common cases (like the source being a `List<T>`), it was actually more efficient to use `Count() != 0` than it was to use `Any()`. While adding an interface check has some overhead, it was worthwhile adding it to make the `Any()` implementation predictable and consistent with `Count()`, such that they could be more easily reasoned about and such that the prevailing wisdom about their costs would become correct.

## Networking

Networking is a critical component of almost any application these days, and great networking performance is of paramount important. As such, every release of .NET now sees a lot of attention paid to improving networking performance, and .NET 5 is no exception.

Let’s start by looking at some primitives and working our way up. `System.Uri` is used by most any app to represent urls, and it’s important that it be fast. A multitude of PRs have gone into making `Uri` much faster in .NET 5. Arguably the most important operation for a `Uri` is constructing one, and [dotnet/runtime#36915](https://github.com/dotnet/runtime/pull/36915) made that faster for all `Uri`s, primarily just by paying attention to overheads and not incurring unnecessary costs:

```
[Benchmark]
public Uri Ctor() => new Uri("https://github.com/dotnet/runtime/pull/36915");
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Ctor | .NET FW 4.8 | 443.2 ns | 1.00 | 225 B |
| Ctor | .NET Core 3.1 | 192.3 ns | 0.43 | 72 B |
| Ctor | .NET 5.0 | 129.9 ns | 0.29 | 56 B |

After construction, it’s very common for applications to access the various components of a `Uri`, and that has been improved as well. In particular, it’s common with a type like `HttpClient` to have a single `Uri` that’s used repeatedly for issuing requests. The `HttpClient` implementation will access the `Uri.PathAndQuery` property in order to send that as part of the HTTP request (e.g. `GET /dotnet/runtime HTTP/1.1`), and in the past that meant recreating a string for that portion of the `Uri` on every request. Thanks to [dotnet/runtime#36460](https://github.com/dotnet/runtime/pull/36460), that is now cached (as is the `IdnHost`):

```
private Uri _uri = new Uri("http://github.com/dotnet/runtime");

[Benchmark]
public string PathAndQuery() => _uri.PathAndQuery;
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| PathAndQuery | .NET FW 4.8 | 17.936 ns | 1.00 | 56 B |
| PathAndQuery | .NET Core 3.1 | 30.891 ns | 1.72 | 56 B |
| PathAndQuery | .NET 5.0 | 2.854 ns | 0.16 | – |

Beyond that, there are a myriad of ways code interacts with `Uri`s, many of which have been improved. For example, [dotnet/corefx#41772](https://github.com/dotnet/corefx/pull/41772) improved `Uri.EscapeDataString` and `Uri.EscapeUriString`, which escape a string according to [RFC 3986](https://tools.ietf.org/html/rfc3986) and [RFC 3987](https://tools.ietf.org/html/rfc3987). Both of these methods relied on a shared helper that employed `unsafe` code, that roundtripped through a `char[]`, and that had a lot of complexity around Unicode handling. This PR rewrote that helper to utilize newer features of .NET, like spans and [runes](https://docs.microsoft.com/en-us/dotnet/api/system.text.rune), in order to make the escape operation both safe and fast. For some inputs, the gains are modest, but for inputs involving Unicode or even for long ASCII inputs, the gains are significant.

```
[Params(false, true)]
public bool ASCII { get; set; }

[GlobalSetup]
public void Setup()
{
    _input = ASCII ?
        new string('s', 20_000) :
        string.Concat(Enumerable.Repeat("\xD83D\xDE00", 10_000));
}

private string _input;

[Benchmark] public string Escape() => Uri.EscapeDataString(_input);
```

| Method | Runtime | ASCII | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- | --- |
| Escape | .NET FW 4.8 | False | 6,162.59 us | 1.00 | 60616272 B |
| Escape | .NET Core 3.1 | False | 6,483.85 us | 1.06 | 60612025 B |
| Escape | .NET 5.0 | False | 243.09 us | 0.04 | 240045 B |
|  |  |  |  |  |  |
| Escape | .NET FW 4.8 | True | 86.93 us | 1.00 | – |
| Escape | .NET Core 3.1 | True | 122.06 us | 1.40 | – |
| Escape | .NET 5.0 | True | 14.04 us | 0.16 | – |

[dotnet/corefx#42225](https://github.com/dotnet/corefx/pull/42225) provides corresponding improvements for `Uri.UnescapeDataString`. The change included using the already vectorized `IndexOf` rather than a manual, pointer-based loop, in order to determine the first location of a character that needs to be unescaped, and then on top of that avoiding some unnecessary code and employing stack allocation instead of heap allocation when feasible. While it helped to make all operations faster, the biggest gains came for strings which had nothing to unescape, meaning the `EscapeDataString` operation had nothing to escape and just returned its input unmodified (this condition was also subsequently helped further by [dotnet/corefx#41684](https://github.com/dotnet/corefx/pull/41684), which enabled the original strings to be returned when no changes were required):

```
private string _value = string.Concat(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 20));

[Benchmark]
public string Unescape() => Uri.UnescapeDataString(_value);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Unescape | .NET FW 4.8 | 847.44 ns | 1.00 |
| Unescape | .NET Core 3.1 | 846.84 ns | 1.00 |
| Unescape | .NET 5.0 | 21.84 ns | 0.03 |

[dotnet/runtime#36444](https://github.com/dotnet/runtime/pull/36444) and [dotnet/runtime#32713](https://github.com/dotnet/runtime/pull/32713) made it faster to compare `Uri`s, and to perform related operations like putting them into dictionaries, especially for relative `Uri`s.

```
private Uri[] _uris = Enumerable.Range(0, 1000).Select(i => new Uri($"/some/relative/path?ID={i}", UriKind.Relative)).ToArray();

[Benchmark]
public int Sum()
{
    int sum = 0;

    foreach (Uri uri in _uris)
        sum += uri.GetHashCode();
        
    return sum;
}
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Sum | .NET FW 4.8 | 330.25 us | 1.00 |
| Sum | .NET Core 3.1 | 47.64 us | 0.14 |
| Sum | .NET 5.0 | 18.87 us | 0.06 |

Moving up the stack, let’s look at `System.Net.Sockets`. Since the inception of .NET Core, the [TechEmpower benchmarks](https://www.techempower.com/benchmarks/#section=data-r19&hw=ph&test=plaintext) have been used as one way of gauging progress. Previously we focused primarily on the “Plaintext” benchmark, which has a particular set of very low-level performance characteristics, but for this release, we wanted to focus on improving two other benchmarks, “JSON Serialization” and “Fortunes” (the latter involves database access, and despite its name, the costs of the former are primarily about networking speed due to a very small JSON payload involved). Our efforts here were primarily on Linux. And when I say “our”, I’m not just referring to folks that work on the .NET team itself; we had a very productive collaborative effort via a working group that spanned folks beyond the core team, such as with great ideas and contributions from [@tmds](https://github.com/tmds) from Red Hat and [@benaadams](https://github.com/benaadams) from Illyriad Games.

On Linux, the `Sockets` implementation is based on [epoll](https://en.wikipedia.org/wiki/Epoll). To achieve the huge scale demanded of many services, we can’t just dedicate a thread per `Socket`, which is where we’d be if blocking I/O were employed for all operations on the Socket. Instead, non-blocking I/O is used, and when the operating system isn’t ready to fulfill a request (e.g. when `ReadAsync` is used on a `Socket` but there’s no data available to read, or when `SendAsync` is used on a `Socket` but there’s no space available in the kernel’s send buffer), epoll is used to notify the `Socket` implementation of a change in the socket’s status so that the operation can be tried again. epoll is a way of using one thread to block efficiently waiting for changes on any number of sockets, and so the implementation maintains a dedicated thread for waiting for changes on all of the `Socket`s registered with that epoll. The implementation maintained multiple epoll threads, generally a number equal to half the number of cores in the system. With multiple `Socket`s all multiplexed onto the same epoll and epoll thread, the implementation needs to be very careful not to run arbitrary work in response to a socket notification; doing so would happen on the epoll thread itself, and thus the epoll thread wouldn’t be able to process further notifications until that work completed. Worse, if that work blocked waiting for another notification on any of the `Socket`s associated with that same epoll, the system would deadlock. As such, the thread processing the epoll tried to do as little work as possible in response to a socket notification, extracting just enough information to queue the actual processing to the thread pool.

It turns out that there was an interesting feedback loop happening between these epoll threads and the thread pool. There was just enough overhead in queueing the work items from the epoll threads that multiple epoll threads were warranted, but multiple epoll threads resulted in some contention on that queueing, such that every additional thread added more than its fair share of overhead. On top of that, the rate of queueing was just low enough that the thread pool would have trouble keeping all of its threads saturated in the case where a very small amount of work would happen in response to a socket operation (which is the case with the JSON serialization benchmark); this would in turn result in the thread pool spending more time sequestering and releasing threads, which made it slower, which created a feedback loop. Long story short, less-than-ideal queueing led to slower processing and more epoll threads than truly needed. This was rectified with two PRs, [dotnet/runtime#35330](https://github.com/dotnet/runtime/pull/35330) and [dotnet/runtime#35800](https://github.com/dotnet/runtime/pull/35800). #35330 changed the queueing model from the epoll threads such that rather than queueing one work item per event (when the epoll wakes up in response to a notification, there may actually be multiple notifications across all of the sockets registered with it, and it will provide all of those notifications in a batch), it would queue one work item for the whole batch. The pool thread processing it then employs a model very much like how `Parallel.For/ForEach` have worked for years, which is that the queued work item can reserve a single item for itself and then queue a replica of itself to help process the remainder. This changes the calculus such that, on most reasonable sized machines, it actually becomes beneficial to have fewer epoll threads rather than more (and, not coincidentally, we want there to be fewer), so #35800 then changes the number of epoll threads used such that there typically ends up just being one (on machines with much larger core counts, there may still be more). We also made the epoll count configurable via the `DOTNET_SYSTEM_NET_SOCKETS_THREAD_COUNT` environment variable, which can be set to the desired count in order to override the system’s defaults if a developer wants to experiment with other counts and provide feedback on their results for their given workload.

As an experiment, in [dotnet/runtime#37974](https://github.com/dotnet/runtime/pull/37974) from [@tmds](https://github.com/tmds) we’ve also added an experimental mode (triggered by setting the `DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS` environment variable to `1` on Linux) where we avoid queueing work to the thread pool at all, and instead just run all socket continuations (e.g. the `Work()` in `await socket.ReadAsync(); Work();`); on the epoll threads. _Hic sunt dracones_! If a socket continuation stalls, no other work associated with that epoll thread will be processed. Worse, if that continuation actually synchronously blocks waiting for other work associated with that epoll, the system will deadlock. However, it’s possible a well-crafted program could achieve better performance in this mode, as the locality of processing could be better and the overhead of queueing to the thread pool could be avoided. Since all sockets work is then run on the epoll threads, it no longer makes sense to default to one; instead it defaults to a number of threads equal to the number of processors. Again, this is an experiment, and we’d welcome feedback on any positive or negative results you see.

There were some other impactful changes as well. In [dotnet/runtime#36371](https://github.com/dotnet/runtime/pull/36371), [@tmds](https://github.com/tmds) changed some of the syscalls used for send and receive operations. In the name of simplicity, the original implementation used the `sendmsg` and `recvmsg` syscalls for sending and receiving on sockets, regardless of how many buffers of data were being provided (these operations support vectored I/O, where multiple buffers rather than just one can be passed to each method). It turns out that there’s measurable overhead in doing so when there’s just one buffer, and #36371 was able to reduce the overhead of typical `SendAsync` and `ReceiveAsync` operations by preferring to use the `send` and `recv` syscalls when appropriate. In [dotnet/runtime#36705](https://github.com/dotnet/runtime/pull/36705) [@tmds](https://github.com/tmds) also changed how requests for socket operations are handled to use a lock-free rather than lock-based approach, in order to reduce some overheads. And in [dotnet/runtime#36997](https://github.com/dotnet/runtime/pull/36997), [@benaadams](https://github.com/benaadams) removed some interface casts that were showing up as measureable overhead in the sockets implementation.

These improvements are all focused on sockets performance on Linux at scale, making them difficult to demonstrate in a microbenchmark on a single machine. There are other improvements, however, that are easier to see. [dotnet/runtime#32271](https://github.com/dotnet/runtime/pull/32271) removed several allocations from `Socket.Connect`, `Socket.Bind`, and a few other operations, where unnecessary copies were being made of some state in support of old Code Access Security (CAS) checks that are no longer relevant: the CAS checks were removed long ago, but the clones remained, so this just cleans those up, too. [dotnet/runtime#32275](https://github.com/dotnet/runtime/pull/32275) also removed an allocation from the Windows implementation of `SafeSocketHandle`. [dotnet/runtime#787](https://github.com/dotnet/runtime/pull/787) refactored `Socket.ConnectAsync` so that it could share the same internal `SocketAsyncEventArgs` instance that ends up being used subsequently to perform `ReceiveAsync` operations, thereby avoiding extra allocations for the connect. [dotnet/runtime#34175](https://github.com/dotnet/runtime/pull/34175) utilizes the new Pinned Object Heap introduced in .NET 5 to use pre-pinned buffers in various portions of the `SocketAsyncEventArgs` implementation on Windows instead of having to use a `GCHandle` to pin (the corresponding functionality on Linux doesn’t require pinning, so it’s not used there). And in [dotnet/runtime#37583](https://github.com/dotnet/runtime/pull/37583), [@tmds](https://github.com/tmds) reduced allocations as part of the vectored I/O `SendAsync`/`ReceivedAsync` implementations on Unix by employing stack allocation where appropriate.

```
private Socket _listener, _client, _server;
private byte[] _buffer = new byte[8];
private List<ArraySegment<byte>> _buffers = new List<ArraySegment<byte>>();

[GlobalSetup]
public void Setup()
{
    _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    _listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    _listener.Listen(1);

    _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    _client.Connect(_listener.LocalEndPoint);

    _server = _listener.Accept();

    for (int i = 0; i < _buffer.Length; i++)
        _buffers.Add(new ArraySegment<byte>(_buffer, i, 1));
}

[Benchmark]
public async Task SendReceive()
{
    await _client.SendAsync(_buffers, SocketFlags.None);
    int total = 0;
    while (total < _buffer.Length)
        total += await _server.ReceiveAsync(_buffers, SocketFlags.None);
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| SendReceive | .NET Core 3.1 | 5.924 us | 1.00 | 624 B |
| SendReceive | .NET 5.0 | 5.230 us | 0.88 | 144 B |

On top of that, we come to `System.Net.Http`. A bunch of improvements were made to `SocketsHttpHandler`, in two areas in particular. The first is the processing of headers, which represents a significant portion of allocations and processing associated with the type. [dotnet/corefx#41640](https://github.com/dotnet/corefx/pull/41640) kicked things off by making the `HttpHeaders.TryAddWithoutValidation` true to its name: due to how `SocketsHttpHandler` was enumerating request headers to write them to the wire, it ended up performing the validation on the headers even though the developer specified “WithoutValidation”, and the PR fixed that. Multiple PRs, including [dotnet/runtime#35003](https://github.com/dotnet/runtime/pull/35003), [dotnet/runtime#34922](https://github.com/dotnet/runtime/pull/34922), [dotnet/runtime#32989](https://github.com/dotnet/runtime/pull/32989), and [dotnet/runtime#34974](https://github.com/dotnet/runtime/pull/34974) improved lookups in `SocketHttpHandler`‘s list of known headers (which helps avoid allocations when such headers are present) and augmented that list to be more comprehensive. [dotnet/runtime#34902](https://github.com/dotnet/runtime/pull/34902) updated the internal collection type used in various strongly-typed header collections to incur less allocation, and [dotnet/runtime#34724](https://github.com/dotnet/runtime/pull/34724) made some of the allocations associated with headers pay-for-play only when they’re actually accessed (and also special-cased Date and Server response headers to avoid allocations for them in the most common cases). The net result is a small improvement to throughput but a significant improvement to allocation:

```
private static readonly Socket s_listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
private static readonly HttpClient s_client = new HttpClient();
private static Uri s_uri;

[Benchmark]
public async Task HttpGet()
{
    var m = new HttpRequestMessage(HttpMethod.Get, s_uri);
    m.Headers.TryAddWithoutValidation("Authorization", "ANYTHING SOMEKEY");
    m.Headers.TryAddWithoutValidation("Referer", "http://someuri.com");
    m.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/77.0.3865.90 Safari/537.36");
    m.Headers.TryAddWithoutValidation("Host", "www.somehost.com");
    using (HttpResponseMessage r = await s_client.SendAsync(m, HttpCompletionOption.ResponseHeadersRead))
    using (Stream s = await r.Content.ReadAsStreamAsync())
        await s.CopyToAsync(Stream.Null);
}

[GlobalSetup]
public void CreateSocketServer()
{
    s_listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    s_listener.Listen(int.MaxValue);
    var ep = (IPEndPoint)s_listener.LocalEndPoint;
    s_uri = new Uri($"http://{ep.Address}:{ep.Port}/");
    byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nDate: Sun, 05 Jul 2020 12:00:00 GMT \r\nServer: Example\r\nContent-Length: 5\r\n\r\nHello");
    byte[] endSequence = new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };

    Task.Run(async () =>
    {
        while (true)
        {
            Socket s = await s_listener.AcceptAsync();
            _ = Task.Run(() =>
            {
                using (var ns = new NetworkStream(s, true))
                {
                    byte[] buffer = new byte[1024];
                    int totalRead = 0;
                    while (true)
                    {
                        int read =  ns.Read(buffer, totalRead, buffer.Length - totalRead);
                        if (read == 0) return;
                        totalRead += read;
                        if (buffer.AsSpan(0, totalRead).IndexOf(endSequence) == -1)
                        {
                            if (totalRead == buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
                            continue;
                        }

                        ns.Write(response, 0, response.Length);

                        totalRead = 0;
                    }
                }
            });
        }
    });
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| HttpGet | .NET FW 4.8 | 123.67 us | 1.00 | 98.48 KB |
| HttpGet | .NET Core 3.1 | 68.57 us | 0.55 | 6.07 KB |
| HttpGet | .NET 5.0 | 66.80 us | 0.54 | 2.86 KB |

Some other header-related PRs were more specialized. For example, [dotnet/runtime#34860](https://github.com/dotnet/runtime/pull/34860) improved parsing of the Date header just by being more thoughtful about the approach. The previous implementation was using `DateTime.TryParseExact` with a long list of viable formats; that knocks the implementation off its fast path and causes it to be much slower to parse even when the input matches the first format in the list. And in the case of Date headers today, the vast majority of headers will follow the format outlined in [RFC 1123](https://tools.ietf.org/html/rfc1123), aka “r”. Thanks to improvements in previous releases, `DateTime`‘s parsing of the “r” format is very fast, so we can just try that one directly first with the `TryParseExact` for a single format, and only if it fails fall back to the `TryParseExact` with the remainder.

```
[Benchmark]
public DateTimeOffset? DatePreferred()
{
    var m = new HttpResponseMessage();
    m.Headers.TryAddWithoutValidation("Date", "Sun, 06 Nov 1994 08:49:37 GMT");
    return m.Headers.Date;
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| DatePreferred | .NET FW 4.8 | 2,177.9 ns | 1.00 | 674 B |
| DatePreferred | .NET Core 3.1 | 1,510.8 ns | 0.69 | 544 B |
| DatePreferred | .NET 5.0 | 267.2 ns | 0.12 | 520 B |

The biggest improvements, however, came for HTTP/2 in general. In .NET Core 3.1, the HTTP/2 implementation was functional, but not particularly tuned, and so some effort for .NET 5 went into making the HTTP/2 implementation better, and in particular more scalable. [dotnet/runtime#32406](https://github.com/dotnet/runtime/pull/32406) and [dotnet/runtime#32624](https://github.com/dotnet/runtime/pull/32624) significantly reduced allocations involved in HTTP/2 GET requests by employing a custom `CopyToAsync` override on the response stream used for HTTP/2 responses, by being more careful around how request headers are accessed as part of writing out the request (in order to avoid forcing lazily-initialized state into existence when it’s not necessary), and removing async-related allocations. And [dotnet/runtime#32557](https://github.com/dotnet/runtime/pull/32557) reduced allocations in HTTP/2 POST requests by being better about how cancellation was handled and reducing allocation associated with async operations there, too. On top of those, [dotnet/runtime#35694](https://github.com/dotnet/runtime/pull/35694) included a bunch of HTTP/2-related changes, including reducing the number of locks involved (HTTP/2 involves more synchronization in the C# implementation than HTTP/1.1, because in HTTP/2 multiple requests are multiplexed onto the same socket connection), reducing the amount of work done while holding locks, in one key case changing the kind of locking mechanism used, adding more headers to the known headers optimization, and a few other tweaks to reduce overheads. As a follow-up, [dotnet/runtime#36246](https://github.com/dotnet/runtime/pull/36246) removed some allocations due to cancellation and trailing headers (which are common in gRPC traffic). To demo this, I created a simple ASP.NET Core localhost server (using the Empty template and removing a small amount of code not needed for this example):

```
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

public class Program
{
    public static void Main(string[] args) =>
        Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(b => b.UseStartup<Startup>()).Build().Run();
}

public class Startup
{
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/", context => context.Response.WriteAsync("Hello"));
            endpoints.MapPost("/", context => context.Response.WriteAsync("Hello"));
        });
    }
}
```

Then I used this client benchmark:

```
private HttpMessageInvoker _client = new HttpMessageInvoker(new SocketsHttpHandler() { UseCookies = false, UseProxy = false, AllowAutoRedirect = false });
private HttpRequestMessage _get = new HttpRequestMessage(HttpMethod.Get, new Uri("https://localhost:5001/")) { Version = HttpVersion.Version20 };
private HttpRequestMessage _post = new HttpRequestMessage(HttpMethod.Post, new Uri("https://localhost:5001/")) { Version = HttpVersion.Version20, Content = new ByteArrayContent(Encoding.UTF8.GetBytes("Hello")) };

[Benchmark] public Task Get() => MakeRequest(_get);

[Benchmark] public Task Post() => MakeRequest(_post);

private Task MakeRequest(HttpRequestMessage request) => Task.WhenAll(Enumerable.Range(0, 100).Select(async _ =>
{
    for (int i = 0; i < 500; i++)
    {
        using (HttpResponseMessage r = await _client.SendAsync(request, default))
        using (Stream s = await r.Content.ReadAsStreamAsync())
            await s.CopyToAsync(Stream.Null);
    }
}));
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Get | .NET Core 3.1 | 1,267.4 ms | 1.00 | 122.76 MB |
| Get | .NET 5.0 | 681.7 ms | 0.54 | 74.01 MB |
|  |  |  |  |  |
| Post | .NET Core 3.1 | 1,464.7 ms | 1.00 | 280.51 MB |
| Post | .NET 5.0 | 735.6 ms | 0.50 | 132.52 MB |

Note, too, that there’s still work being done in this area for .NET 5. [dotnet/runtime#38774](https://github.com/dotnet/runtime/pull/39166) changes how writes are handled in the HTTP/2 implementation and is expected to bring substantial scalability gains over the improvements that have already gone in, in particular for gRPC-based workloads.

There were notable improvements to other networking components as well. For example, the `XxAsync` APIs on the `Dns` type had been implemented on top of the corresponding `Begin/EndXx` methods. For .NET 5 in [dotnet/corefx#41061](https://github.com/dotnet/corefx/pull/41061), that was inverted, such that the `Begin/EndXx` methods were implemented on top of the `XxAsync` ones; that made the code simpler and a bit faster, while also having a nice impact on allocation (note that the .NET Framework 4.8 result is slightly faster because it’s not actually using async I/O, and rather just a queued work item to the `ThreadPool` that performs synchronous I/O; that results in a bit less overhead but also less scalability):

```
private string _hostname = Dns.GetHostName();

[Benchmark] public Task<IPAddress[]> Lookup() => Dns.GetHostAddressesAsync(_hostname);
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Lookup | .NET FW 4.8 | 178.6 us | 1.00 | 4146 B |
| Lookup | .NET Core 3.1 | 211.5 us | 1.18 | 1664 B |
| Lookup | .NET 5.0 | 209.7 us | 1.17 | 984 B |

And while it’s a lesser-used type (though it is used by WCF), `NegotiateStream` was also similarly updated in [dotnet/runtime#36583](https://github.com/dotnet/runtime/pull/36583), with all of its `XxAsync` methods re-implemented to use `async`/`await`, and then in [dotnet/runtime#37772](https://github.com/dotnet/runtime/pull/37772) to reuse buffers rather than create new ones for each operation. The net result is significantly less allocation in typical read/write usage:

```
private byte[] _buffer = new byte[1];
private NegotiateStream _client, _server;

[GlobalSetup]
public void Setup()
{
    using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    listener.Listen(1);

    var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    client.Connect(listener.LocalEndPoint);

    Socket server = listener.Accept();

    _client = new NegotiateStream(new NetworkStream(client, true));
    _server = new NegotiateStream(new NetworkStream(server, true));

    Task.WaitAll(
        _client.AuthenticateAsClientAsync(),
        _server.AuthenticateAsServerAsync());
}

[Benchmark]
public async Task WriteRead()
{
    for (int i = 0; i < 100; i++)
    {
        await _client.WriteAsync(_buffer);
        await _server.ReadAsync(_buffer);
    }
}

[Benchmark]
public async Task ReadWrite()
{
    for (int i = 0; i < 100; i++)
    {
        var r = _server.ReadAsync(_buffer);
        await _client.WriteAsync(_buffer);
        await r;
    }
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| WriteRead | .NET Core 3.1 | 1.510 ms | 1.00 | 61600 B |
| WriteRead | .NET 5.0 | 1.294 ms | 0.86 | – |
|  |  |  |  |  |
| ReadWrite | .NET Core 3.1 | 3.502 ms | 1.00 | 76224 B |
| ReadWrite | .NET 5.0 | 3.301 ms | 0.94 | 226 B |

## JSON

There were significant improvements made to the `System.Text.Json` library for .NET 5, and in particular for `JsonSerializer`, but many of those improvements were actually ported back to `.NET Core 3.1` and released as part of servicing fixes (see [dotnet/corefx#41771](https://github.com/dotnet/corefx/pull/41771)). Even so, there are some nice improvements that show up in .NET 5 beyond those.

[dotnet/runtime#2259](https://github.com/dotnet/runtime/pull/2259) refactored the model for how converters in the `JsonSerializer` handle collections, resulting in measurable improvements, in particular for larger collections:

```
private MemoryStream _stream = new MemoryStream();
private DateTime[] _array = Enumerable.Range(0, 1000).Select(_ => DateTime.UtcNow).ToArray();

[Benchmark]
public Task LargeArray()
{
    _stream.Position = 0;
    return JsonSerializer.SerializeAsync(_stream, _array);
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| LargeArray | .NET FW 4.8 | 262.06 us | 1.00 | 24256 B |
| LargeArray | .NET Core 3.1 | 191.34 us | 0.73 | 24184 B |
| LargeArray | .NET 5.0 | 69.40 us | 0.26 | 152 B |

but even for smaller ones, e.g.

```
private MemoryStream _stream = new MemoryStream();
private JsonSerializerOptions _options = new JsonSerializerOptions();
private Dictionary<string, int> _instance = new Dictionary<string, int>()
{
    { "One", 1 }, { "Two", 2 }, { "Three", 3 }, { "Four", 4 }, { "Five", 5 },
    { "Six", 6 }, { "Seven", 7 }, { "Eight", 8 }, { "Nine", 9 }, { "Ten", 10 },
};

[Benchmark]
public async Task Dictionary()
{
    _stream.Position = 0;
    await JsonSerializer.SerializeAsync(_stream, _instance, _options);
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Dictionary | .NET FW 4.8 | 2,141.7 ns | 1.00 | 209 B |
| Dictionary | .NET Core 3.1 | 1,376.6 ns | 0.64 | 208 B |
| Dictionary | .NET 5.0 | 726.1 ns | 0.34 | 152 B |

[dotnet/runtime#37976](https://github.com/dotnet/runtime/pull/37976) also helped improve the performance of small types by adding a layer of caching to help retrieve the metadata used internally for the type being serialized and deserialized.

```
private MemoryStream _stream = new MemoryStream();
private MyAwesomeType _instance = new MyAwesomeType() { SomeString = "Hello", SomeInt = 42, SomeByte = 1, SomeDouble = 1.234 };

[Benchmark]
public Task SimpleType()
{
    _stream.Position = 0;
    return JsonSerializer.SerializeAsync(_stream, _instance);
}

public struct MyAwesomeType
{
    public string SomeString { get; set; }
    public int SomeInt { get; set; }
    public double SomeDouble { get; set; }
    public byte SomeByte { get; set; }
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| SimpleType | .NET FW 4.8 | 1,204.3 ns | 1.00 | 265 B |
| SimpleType | .NET Core 3.1 | 617.2 ns | 0.51 | 192 B |
| SimpleType | .NET 5.0 | 504.2 ns | 0.42 | 192 B |

## Trimming

Up until .NET Core 3.0, .NET Core was focused primarily on server workloads, with ASP.NET Core being the preeminent application model on the platform. With .NET Core 3.0, Windows Forms and Windows Presentation Foundation (WPF) were added, bringing .NET Core to desktop applications. With .NET Core 3.2, Blazor support for browser applications was released, but based on mono and the library’s from the mono stack. With .NET 5, Blazor uses the .NET 5 mono runtime and all of the same .NET 5 libraries shared by every other app model. This brings an important twist to performance: size. While code size has always been an important issue (and is important for .NET Native applications), the scale required for a successful browser-based deployment really brings it to the forefront, as we need to be concerned about download size in a way we haven’t focused with .NET Core in the past.

To assist with application size, the .NET SDK includes a [linker](https://github.com/mono/linker) that’s capable of trimming away unused portions of the app, not only at the assembly level, but also at the member level, doing static analysis to determine what code is and isn’t used and throwing away the parts that aren’t. This brings an interesting set of challenges: some coding patterns employed for convenience or simplified API consumption are difficult for the linker to analyze in a way that would allow it to throw away much of anything. As a result, one of the big performance-related efforts in .NET 5 is around improving the trimmability of the libraries.

There are two facets to this:

1.  Not removing too much (correctness). We need to make sure that the libraries can actually be trimmed safely. In particular, reflection (even reflection only over public surface area) makes it difficult for the linker to find all members that may actually be used, e.g. code in one place in the app uses `typeof` to get a `Type` instance, and passes that to another part of the app that uses `GetMethod` to retrieve a `MethodInfo` for a public method on that type, and passes that `MethodInfo` to another part of the app which invokes it. To address that, the linker employs heuristics to minimize false positives on APIs that can be removed, but to help it further, a bunch of attributes have been added in .NET 5 that enable developers to make such implicit dependencies explicit, to suppress warnings from the linker on things it might deem to be unsafe but actually aren’t, and to force warnings onto consumers to say that certain portions of the surface area simply aren’t amenable to linking. See [dotnet/runtime#35387](https://github.com/dotnet/runtime/pull/35387).
2.  Removing as much as possible (performance). We need to minimize the reasons why pieces of code need to be kept around. This can manifest as refactoring implementations to change calling patterns, it can manifest as using conditions the linker can recognize and use to trim out whole swaths of code, and it can manifest as using finer-grained controls over exactly what needs to be kept and why.

There are many examples of the second, so I’ll highlight a few to showcase the various techniques employed:

-   Removing unnecessary code, such as in [dotnet/corefx#41177](https://github.com/dotnet/corefx/pull/41177). Here we find a lot of antiquated `TraceSource`/`Switch` usage, which only existed to enable some debug-only tracing and asserts, but which no one was actually using anymore, and which were causing some of these types to be seen by the linker as used even in release builds.
-   Removing antiquated code that once served a purpose but no longer does, such as in [dotnet/coreclr#26750](https://github.com/dotnet/coreclr/pull/26750). This type used to be important to help improve ngen (the predecessor of crossgen), but it’s no longer needed. Or such as in [dotnet/coreclr#26603](https://github.com/dotnet/coreclr/pull/26603), where some code was no longer actually used, but was causing types to be kept around nonetheless.
-   Removing duplicate code, such as in [dotnet/corefx#41165](https://github.com/dotnet/corefx/pull/41165), [dotnet/corefx#40935](https://github.com/dotnet/corefx/pull/40935), and [dotnet/coreclr#26589](https://github.com/dotnet/coreclr/pull/26589). Several libraries were using their own private copy of some hash code helper routines, resulting in each having its own copy of IL for that functionality. They could instead be updated to use the shared `HashCode` type, which not only helps in IL size and trimming, but also helps to avoid extra code that needs to be maintained and to better modernize the codebase to utilize the functionality we’re recommending others use as well.
-   Using different APIs, such as in [dotnet/corefx#41143](https://github.com/dotnet/corefx/pull/41143). Code was using extension helper methods that were resulting in additional types being pulled in, but the “help” provided actually saved little-to-no code. A potentially better example is [dotnet/corefx#41142](https://github.com/dotnet/corefx/pull/41142), which removed use of the non-generic `Queue` and `Stack` types from the `System.Xml` implementations, instead using only the generic implementations ([dotnet/coreclr#26597](https://github.com/dotnet/coreclr/pull/26597) did something similar, with `WeakReference`). Or [dotnet/corefx#41111](https://github.com/dotnet/corefx/pull/41111), which changed some code in the XML library to use `HttpClient` rather than `WebRequest`, which allowed removing the entire `System.Net.Requests` dependency. Or [dotnet/corefx#41110](https://github.com/dotnet/corefx/pull/41110), which avoided `System.Net.Http` needing to use `System.Text.RegularExpressions`: it was unnecessary complication that could be replaced with a tiny amount of code specific to that use case. Another example is [dotnet/coreclr#26602](https://github.com/dotnet/coreclr/pull/26602), where some code was unnecessarily using `string.ToLower()`, and replacing its usage was not only more efficient, it helped to enable that overload to be trimmed away by default. [dotnet/coreclr#26601](https://github.com/dotnet/coreclr/pull/26601) is similar.
-   Rerouting logic to avoid rooting large swaths of unneeded code, such as in [dotnet/corefx#41075](https://github.com/dotnet/corefx/pull/41075). If code just used `new Regex(string)`, that internally just delegated to the longer `Regex(string, RegexOptions)` constructor, and that constructor needs to be able to use the internal `RegexCompiler` in case the `RegexOptions.Compiled` is used. By tweaking the code paths such that the `Regex(string)` constructor doesn’t depend on the `Regex(string, RegexOptions)` constructor, it becomes trivial for the linker to remove the whole `RegexCompiler` code path (and its dependency on reflection emit) if it’s not otherwise used. [dotnet/corefx#41101](https://github.com/dotnet/corefx/pull/41101) then took better advantage of this by ensuring the shorter calls could be used when possible. This is a fairly common pattern for avoiding such unnecessary rooting. Consider `Environment.GetEnvironmentVariable(string)`. It used to call to the `Environment.GetEnvironmentVariable(string, EnvironmentVariableTarget)` overload, passing in the default `EnvironmentVariableTarget.Process`. Instead, the dependency was inverted: the `Environment.GetEnvironmentVariable(string)` overload contains only the logic for handling the `Process` case, and then the longer overload has `if (target == EnvironmentVariableTarget.Process) return GetEnvironmentVariable(name);`. That way, the most common case of just using the simple overload doesn’t pull in all of the code paths necessary to handle the other much less common targets. [dotnet/corefx#0944](https://github.com/dotnet/corefx/pull/40944) is another example: for apps that just write to the console rather than also read from the console, it enables a lot more of the console internals to be linked away.
-   Using lazy initialization, especially for static fields, such as in [dotnet/runtime#37909](https://github.com/dotnet/runtime/pull/37909). If a type is used and any of its static methods are called, its static constructor will need to be kept, and any fields initialized by the static constructor will also need to be kept. If such fields are instead lazily initialized on first use, the fields will only need to be kept if the code that performs that lazy initialization is reachable.
-   Using feature switches, such as in [dotnet/runtime#38129](https://github.com/dotnet/runtime/pull/38129) (further benefited from in [dotnet/runtime#38828](https://github.com/dotnet/runtime/pull/38828)). In many cases, whole feature sets may not be necessary for an app, such as logging or debugging support, but from the linker’s perspective, it sees the code being used and thus is forced to keep it. However, the linker is capable of being told about replacement values it should use for known properties, e.g. you can tell the linker that when it sees a `Boolean`\-returning `SomeClass.SomeProperty`, it should replace it with a constant false, which will in turn enable it to trim out any code guarded by that property.
-   Ensuring that test-only code is only in tests, as in [dotnet/runtime#38729](https://github.com/dotnet/runtime/pull/38729). In this case, some code intended only to be used for testing was getting compiled into the product assembly, and its tendrils were causing `System.Linq.Expressions` to be brought in as well.

## Peanut Butter

In my [.NET Core 3.0 performance post](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-core-3-0/), I talked about “peanut butter”, lots of small improvements here and there that individually don’t necessarily make a huge difference, but are addressing costs that are otherwise smeared across the code, and fixing a bunch of these en mass can make a measurable difference. As with previous releases, there are a myriad of these welcome improvements that have gone into .NET 5. Here’s a smattering:

-   Faster assembly loading. For historical reasons, .NET Core had a lot of tiny implementation assemblies, with the split serving little meaningful purpose. Yet every additional assembly that needs to be loaded adds overhead. [dotnet/runtime#2189](https://github.com/dotnet/runtime/pull/2189) and [dotnet/runtime#31991](https://github.com/dotnet/runtime/pull/31991) merged a bunch of small assemblies together in order to reduce the number that need to be loaded.
-   Faster math. [dotnet/coreclr#27272](https://github.com/dotnet/coreclr/pull/27272) improved checks for NaN, making the code for `double.IsNan` and `float.IsNan` smaller code and be faster. [dotnet/runtime#35456](https://github.com/dotnet/runtime/pull/35456) from [@john-h-k](https://github.com/john-h-k) is a nice example of using SSE and AMD64 intrinsics to measurably speed up `Math.CopySign` and `MathF.CopySign`. And [dotnet/runtime#34452](https://github.com/dotnet/runtime/pull/34452) from [@Marusyk](https://github.com/Marusyk) improved hash code generation for `Matrix3x2` and `Matrix4x4`.
-   Faster crypto. In place of open-coded equivalents, [dotnet/runtime#36881](https://github.com/dotnet/runtime/pull/36881) from [@vcsjones](https://github.com/vcsjones) used the optimized `BinaryPrimitives` in various places within `System.Security.Cryptography`, yielding more maintainable and faster code, and [dotnet/corefx#39600](https://github.com/dotnet/corefx/pull/39600) from [@VladimirKhvostov](https://github.com/VladimirKhvostov) optimized the out-of-favor-but-still-in-use `CryptoConfig.CreateFromName` method to be upwards of 10x faster.
-   Faster interop. [dotnet/runtime#36257](https://github.com/dotnet/runtime/pull/36257) reduced entrypoint probing (where the runtime tries to find the exact native function to use for a P/Invoke) by avoiding the Windows-specific “ExactSpelling” checks when on Linux and by setting it to true for more methods when on Windows. [dotnet/runtime#33020](https://github.com/dotnet/runtime/pull/33020) from [@NextTurn](https://github.com/NextTurn) used `sizeof(T)` instead of `Marshal.SizeOf(Type)`/`Marshal.SizeOf<T>()` in a bunch of places, as the former has much less overhead than the latter. And [dotnet/runtime#33967](https://github.com/dotnet/runtime/pull/33967), [dotnet/runtime#35098](https://github.com/dotnet/runtime/pull/35098), and [dotnet/runtime#39059](https://github.com/dotnet/runtime/pull/39059) reduced interop and marshaling costs in several libraries by using more blittable types, using spans and ref locals, using `sizeof`, and so on.
-   Faster reflection emit. Reflection emit enables developers to write out IL at run-time, and if you can emit the same instructions in a way that takes up less space, you can save on the managed allocations needed to store the sequence. A variety of IL opcodes have shorter variants for more common cases, e.g. `Ldc_I4` can be used to load any `int` value as a constant, but `Ldc_I4_S` is shorter and can be used to load any `sbyte`, while `Ldc_I4_1` is shorter still and is used to load the value `1`. Some libraries take advantage of this and have their own mapping table as part of their emit code to employ the shortest relevant opcode; others don’t. [dotnet/runtime#35427](https://github.com/dotnet/runtime/pull/35427) just moved such a mapping into the `ILGenerator` itself, enabling us to delete all of the customized implementations in the libraries in dotnet/runtime, and get the benefits of the mapping in all of those and others automatically.
-   Faster I/O. [dotnet/runtime#37705](https://github.com/dotnet/runtime/pull/37705) from [@bbartels](https://github.com/bbartels) improved `BinaryWriter.Write(string)`, giving it a fast path for various common inputs. And [dotnet/runtime#35978](https://github.com/dotnet/runtime/pull/35978) improved how relationships are managed inside `System.IO.Packaging` by using O(1) instead of O(N) lookups.
-   Lots of small allocations here and there. For example, [dotnet/runtime#35005](https://github.com/dotnet/runtime/pull/35005) removes a `MemoryStream` allocation in `ByteArrayContent`, [dotnet/runtime#36228](https://github.com/dotnet/runtime/pull/36228) from [@Youssef1313](https://github.com/Youssef1313) removes a `List<T>` and underlying `T[]` allocation in `System.Reflection.MetadataLoadContext`, [dotnet/runtime#32297](https://github.com/dotnet/runtime/pull/32297) removes a `char[]` allocation in `XmlConverter.StripWhitespace`, [dotnet/runtime#32276](https://github.com/dotnet/runtime/pull/32276) removes a `byte[]` allocation on startup in `EventSource`, [dotnet/runtime#32298](https://github.com/dotnet/runtime/pull/32298) removes a `char[]` allocation in `HttpUtility`, [dotnet/runtime#32299](https://github.com/dotnet/runtime/pull/32299) removes potentially several `char[]`s in `ModuleBuilder`, [dotnet/runtime#32301](https://github.com/dotnet/runtime/pull/32301) removes some `char[]` allocations from `String.Split` usage, [dotnet/runtime#32422](https://github.com/dotnet/runtime/pull/32422) removes a `char[]` allocation in `AsnFormatter`, [dotnet/runtime#34551](https://github.com/dotnet/runtime/pull/34551) removes several string allocations in `System.IO.FileSystem`, [dotnet/corefx#41363](https://github.com/dotnet/corefx/pull/41363) removes a `char[]` allocation in `JsonCamelCaseNamingPolicy`, [dotnet/coreclr#25631](https://github.com/dotnet/coreclr/pull/25631) removes string allocations from `MethodBase.ToString()`, [dotnet/corefx#41274](https://github.com/dotnet/corefx/pull/41274) removes some unnecessary strings from `CertificatePal.AppendPrivateKeyInfo`, [dotnet/runtime#1155](https://github.com/dotnet/runtime/pull/1155) from [@Wraith2](https://github.com/Wraith2) removes temporary arrays from `SqlDecimal` via spans, [dotnet/coreclr#26584](https://github.com/dotnet/coreclr/pull/26584) removed boxing that previously occurred when using methods like `GetHashCode` on some tuples, [dotnet/coreclr#27451](https://github.com/dotnet/coreclr/pull/27451) removed several allocations from reflecting over custom attributes, [dotnet/coreclr#27013](https://github.com/dotnet/coreclr/pull/27013) remove some string allocations from concatenations by replacing some inputs with consts, and [dotnet/runtime#34774](https://github.com/dotnet/runtime/pull/34774) removed some temporary `char[]` allocations from `string.Normalize`.

## New Performance-focused APIs

This post has highlighted a plethora of existing APIs that simply get better when running on .NET 5. In addition, there are lots of new APIs in .NET 5, some of which are focused on helping developers to write faster code (many more are focused on enabling developers to perform the same operations with less code, or on enabling new functionality that wasn’t easily accomplished previously) . Here are a few highlights, including in some cases where the APIs are already being used internally by the rest of the libraries to lower costs in existing APIs:

-   `Decimal(ReadOnlySpan<int>)` / `Decimal.TryGetBits` / `Decimal.GetBits` ([dotnet/runtime#32155](https://github.com/dotnet/runtime/pull/32155)): In previous releases we added lots of span-based methods for efficiently interacting with primitives, and `decimal` did get span-based `TryFormat` and `{Try}Parse` methods, but these new methods in .NET 5 enable efficiently constructing a `decimal` from a span as well as extracting the bits from a `decimal` into a span. You can see this support already being used in `SQLDecimal`, in `BigInteger`, in `System.Linq.Expressions`, and in `System.Reflection.Metadata`.
-   `MemoryExtensions.Sort` ([dotnet/coreclr#27700](https://github.com/dotnet/coreclr/pull/27700)). I talked about this earlier: new `Sort<T>` and `Sort<TKey, TValue>` extension methods enable sorting arbitrary spans of data. These new public methods are already being used in `Array` itself ([dotnet/coreclr#27703](https://github.com/dotnet/coreclr/pull/27703)) as well as in `System.Linq` ([dotnet/runtime#1888](https://github.com/dotnet/runtime/pull/1888)).
-   `GC.AllocateArray<T>` and `GC.AllocateUninitializedArray<T>` ([dotnet/runtime#33526](https://github.com/dotnet/runtime/pull/33526)). These new APIs are like using `new T[length]`, except with two specialized behaviors: using the `Uninitialized` variant lets the GC hand back arrays without forcefully clearing them (unless they contain references, in which case it must clear at least those), and passing `true` to the `bool pinned` argument returns arrays from the new Pinned Object Heap (POH), from which arrays are guaranteed to never be moved in memory such that they can be passed to external code without pinning them (i.e. without using `fixed` or `GCHandle`). `StringBuilder` gained support for using the uninitialized feature ([dotnet/coreclr#27364](https://github.com/dotnet/coreclr/pull/27364)) to reduce the cost of expanding its internal storage, as did the new `TranscodingStream` ([dotnet/runtime#35145](https://github.com/dotnet/runtime/pull/35145)), and even the new support for importing X509 certificates and collections from Privacy Enhanced Mail Certificate (PEM) files ([dotnet/runtime#38280](https://github.com/dotnet/runtime/pull/38280)). You can also see the pinning support being put to good use in the Windows implementation of `SocketsAsyncEventArgs` ([dotnet/runtime#34175](https://github.com/dotnet/runtime/pull/34175)), where it needs to allocate pinned buffers for operations like `ReceiveMessageFrom`.
-   `StringSplitOptions.TrimEntries` ([dotnet/runtime#35740](https://github.com/dotnet/runtime/pull/35740)). `String.Split` overloads accept a `StringSplitOptions` enum that enables `Split` to optionally remove empty entries from the resulting array. The new `TrimEntries` enum value works with or without this option to first trim results. Regardless of whether `RemoveEmptyEntries` is used, this enables `Split` to avoid allocating strings for entries that would become empty once trimmed (or for the allocated strings to be smaller), and then in conjunction with `RemoveEmptyEntries` for the resulting array to be smaller in such cases. Also, it was found to be common for consumers of `Split` to subsequently call `Trim()` on each string, so doing the trimming as part of the `Split` call can eliminate extra string allocations for the caller. This is used in a handful of types and methods in dotnet/runtime, such as by `DataTable`, `HttpListener`, and `SocketsHttpHandler`.
-   `BinaryPrimitives.{Try}{Read/Write}{Double/Single}{Big/Little}Endian` ([dotnet/runtime#6864](https://github.com/dotnet/runtime/pull/6864)). You can see these APIs being used, for example, in the new Concise Binary Object Representation (CBOR) support added in .NET 5 ([dotnet/runtime#34046](https://github.com/dotnet/runtime/pull/34046)).
-   `MailAddress.TryCreate` ([dotnet/runtime#1052](https://github.com/dotnet/runtime/pull/1052) from [@MarcoRossignoli](https://github.com/MarcoRossignoli)) and `PhysicalAddress.{Try}Parse` ([dotnet/runtime#1057](https://github.com/dotnet/runtime/pull/1057)). The new `Try` overloads enable parsing without exceptions, and the span-based overloads enable parsing addresses from within larger contexts without incurring allocations for substrings.
-   `SocketAsyncEventArgs(bool unsafeSuppressExecutionContextFlow)` ([dotnet/runtime#706](https://github.com/dotnet/runtime/pull/706) from [@MarcoRossignoli](https://github.com/MarcoRossignoli)). By default, asynchronous operations in .NET flow `ExecutionContext`, which means call sites implicitly “capture” the current `ExecutionContext` and “restore” it when executing the continuation code. This is how `AsyncLocal<T>` values propagate through asynchronous operations. Such flowing is generally cheap, but there is still a small amount of overhead. As socket operations can be performance-critical, this new constructor on `SocketAsyncEventArgs` constructor can be used when the developer knows that the context won’t be needed in the callbacks raised by the instance. You can see this used, for example, in `SocketHttpHandler`‘s internal `ConnectHelper` ([dotnet/runtime#1381](https://github.com/dotnet/runtime/pull/1381)).
-   `Unsafe.SkipInit<T>` ([dotnet/corefx#41995](https://github.com/dotnet/corefx/pull/41995)). The C# compiler’s [definite assignment](https://en.wikipedia.org/wiki/Definite_assignment_analysis) rules require that parameters and locals be assigned to in a variety of situations. In very specific cases, that can require an extra assignment that isn’t actually needed, which, when counting every instruction and memory-write in performance-sensitive code, can be undesirable. This method effectively enables code to pretend it wrote to the parameter or local without actually having done so. This is used in various operations on `Decimal` ([dotnet/runtime#272377](https://github.com/dotnet/coreclr/pull/27377)), in some of the new APIs on `IntPtr` and `UIntPtr` ([dotnet/runtime#307](https://github.com/dotnet/runtime/pull/307) from [@john-h-k](https://github.com/john-h-k)), in `Matrix4x4` ([dotnet/runtime#36323](https://github.com/dotnet/runtime/pull/36323) from [@eanova](https://github.com/eanova)), in `Utf8Parser` ([dotnet/runtime#33507](https://github.com/dotnet/runtime/pull/33507)), and in `UTF8Encoding` ([dotnet/runtime#31904](https://github.com/dotnet/runtime/pull/31904)).
-   `SuppressGCTransitionAttribute` ([dotnet/coreclr#26458](https://github.com/dotnet/coreclr/pull/26458)). This is an advanced attribute for use with P/Invokes that enables the runtime to suppress the [cooperative-to-preemptive mode transition](https://github.com/dotnet/runtime/blob/4fdf9ff8812869dcf957ce0d2eb07c0d5779d1c6/docs/coding-guidelines/clr-code-guide.md#2.1.8) it would normally incur, as it does when making internal [“FCalls”](https://github.com/dotnet/runtime/blob/4fdf9ff8812869dcf957ce0d2eb07c0d5779d1c6/docs/design/coreclr/botr/corelib.md#calling-from-managed-to-native-code) into the runtime itself. This attribute needs to be used with extreme care (see the [detailed comments](https://github.com/dotnet/runtime/blob/c8a994222d8b6cb4202a85570ee860e4b34a89e9/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/SuppressGCTransitionAttribute.cs#L46-L51) in the attribute’s description). Even so, you can see it’s used by a few methods in Corelib ([dotnet/runtime#27473](https://github.com/dotnet/coreclr/pull/27473)), and there are pending changes for the JIT that will make it even better ([dotnet/runtime#39111](https://github.com/dotnet/runtime/pull/39111)).
-   `CollectionsMarshal.AsSpan` ([dotnet/coreclr#26867](https://github.com/dotnet/coreclr/pull/26867)). This method gives callers span-based access to the backing store of a `List<T>`.
-   `MemoryMarshal.GetArrayDataReference` ([dotnet/runtime#1036](https://github.com/dotnet/runtime/pull/1036)). This method returns a reference to the first element of an array (or to where it would have been if the array wasn’t empty). No validation is performed, so it’s both dangerous and very fast. This method is used in a bunch of places in Corelib, all for very low-level optimizations. For example, it’s used as part of the previously-discussed cast helpers implemented in C# ([dotnet/runtime#1068](https://github.com/dotnet/runtime/pull/1068)) and as part of using `Buffer.Memmove` in various places ([dotnet/runtime#35733](https://github.com/dotnet/runtime/pull/35733)).
-   `SslStreamCertificateContext` ([dotnet/runtime#38364](https://github.com/dotnet/runtime/pull/38364)\]. When `SslStream.AuthenticateAsServer{Async}` is provided with the certificate to use, it tries to build the complete X509 chain, an operation which can have varying amounts of associated cost and even perform I/O if additional certificate information needs to be downloaded. In some circumstances, that could happen for the same certificate used to create any number of `SslStream` instances, resulting in duplicated expense. `SslStreamCertificateContext` serves as a sort of cache for the results of such a computation, with the work able to be performed once in advanced and then passed to `SslStream` for any amount of reuse. This helps to avoid that duplicated effort, while also giving callers more predictability and control over any failures.
-   `HttpClient.Send` ([dotnet/runtime#34948](https://github.com/dotnet/runtime/pull/34948)). It may be strange to some readers to see a synchronous API called out here. While `HttpClient` was designed for asynchronous usage, we have found situations where developers are unable to utilize asynchrony, such as when implementing an interface method that’s only synchronous, or being called from a native operation that requires a response synchronously, yet the need to download data is ubiquitous. In these cases, forcing the developer to perform “sync over async” (meaning performing an asynchronous operation and then blocking waiting for it to complete) performs and scales worse than if a synchronous operation were used in the first place. As such, .NET 5 sees limited new synchronous surface area added to `HttpClient` and its supporting types. dotnet/runtime does itself have use for this in a few places. For example, on Linux when the `X509Certificates` support needs to download a certificate as part of chain building, it is generally on a code path that needs to be synchronous all the way back to an OpenSSL callback; previously this would use `HttpClient.GetByteArrayAsync` and then block waiting for it to complete, but that was shown to cause noticeable scalability problems for some users… [dotnet/runtime#38502](https://github.com/dotnet/runtime/pull/38502) changed it to use the new sync API instead. Similarly, the older `HttpWebRequest` type is built on top of `HttpClient`, and in previous releases of .NET Core, its synchronous `GetResponse()` method was actually doing sync-over-async; as of [dotnet/runtime#39511](https://github.com/dotnet/runtime/pull/38511), it’s now using the synchronous `HttpClient.Send` method.
-   `HttpContent.ReadAsStream` ([dotnet/runtime#37494](https://github.com/dotnet/runtime/pull/37494)). This is logically part of the `HttpClient.Send` effort mentioned above, but I’m calling it out separately because it’s useful on its own. The existing `ReadAsStreamAsync` method is a bit of an oddity. It was originally exposed as async just in case a custom HttpContent-derived type would require that, but it’s extremely rare to find any overrides of `HttpContent.ReadAsStreamAsync` that aren’t synchronous, and the implementation returned from requests made on `HttpClient` are all synchronous. As a result, callers end up paying for the `Task<Stream>` wrapper object for the returned `Stream`, when in practice it’s always immediately available. Thus, the new `ReadAsStream` method can actually be useful in such cases to avoid the extra `Task<Stream>` allocation. You can see it being employed in that manner in dotnet/runtime in various places, such as by the `ClientWebSocket` implementation.
-   Non-generic `TaskCompletionSource` ([dotnet/runtime#37452](https://github.com/dotnet/runtime/pull/37452)). Since `Task` and `Task<T>` were introduced, `TaskCompletionSource<T>` was a way of constructing tasks that would be completed manually by the caller via it’s `{Try}Set` methods. And since `Task<T>` derives from `Task`, the single generic type could be used for both generic `Task<T>` and non-generic `Task` needs. However, this wasn’t always obvious to folks, leading to confusion about the right solution for the non-generic case, compounded by the ambiguity about which type to use for `T` when it was just throw-away. .NET 5 adds a non-generic `TaskCompletionSource`, which not only eliminates the confusion, but helps a bit with performance as well, as it avoids the task needing to carry around space for a useless `T`.
-   `Task.WhenAny(Task, Task)` ([dotnet/runtime#34288](https://github.com/dotnet/runtime/pull/34288) and [dotnet/runtime#37488](https://github.com/dotnet/runtime/pull/37448)). Previously, any number of tasks could be passed to `Task.WhenAny` via its overload that accepts a `params Task[] tasks`. However, in analyzing uses of this method, it was found that vast majority of call sites always passed two tasks. The new public overload optimizes for that case, and a neat thing about this overload is that just recompiling those call sites will cause the compiler to bind to the new faster overload instead of the old one, so no code changes are needed to benefit from the overload.

```
private Task _incomplete = new TaskCompletionSource<bool>().Task;

[Benchmark]
public Task OneAlreadyCompleted() => Task.WhenAny(Task.CompletedTask, _incomplete);

[Benchmark]
public Task AsyncCompletion()
{
    AsyncTaskMethodBuilder atmb = default;
    Task result = Task.WhenAny(atmb.Task, _incomplete);
    atmb.SetResult();
    return result;
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| OneAlreadyCompleted | .NET FW 4.8 | 125.387 ns | 1.00 | 217 B |
| OneAlreadyCompleted | .NET Core 3.1 | 89.040 ns | 0.71 | 200 B |
| OneAlreadyCompleted | .NET 5.0 | 8.391 ns | 0.07 | 72 B |
|  |  |  |  |  |
| AsyncCompletion | .NET FW 4.8 | 289.042 ns | 1.00 | 257 B |
| AsyncCompletion | .NET Core 3.1 | 195.879 ns | 0.68 | 240 B |
| AsyncCompletion | .NET 5.0 | 150.523 ns | 0.52 | 160 B |

-   And too many `System.Runtime.Intrinsics` methods to even begin to mention!

## New Performance-focused Analyzers

The C# “Roslyn” compiler has a very useful extension point called [“analyzers”](https://docs.microsoft.com/en-us/visualstudio/code-quality/roslyn-analyzers-overview), or “Roslyn analyzers”. Analyzers plug into the compiler and are given full read access to all of the source the compiler is operating over as well as the compiler’s parsing and modeling of that code, which enables developers to plug in their own custom analyses to a compilation. On top of that, analyzers are not only runnable as part of builds but also in the IDE as the developer is writing their code, which enables analyzers to present suggestions, warnings, and errors on how the developer may improve their code. Analyzer developers can also author “fixers” that can be invoked in the IDE and automatically replace the flagged code with a “fixed” alternatives. And all of these components can be distributed via NuGet packages, making it easy for developers to consume arbitrary analyses written by others.

The [Roslyn Analyzers](https://github.com/dotnet/roslyn-analyzers) repo contains a bunch of custom analyzers, including ports of the old [FxCop rules](https://docs.microsoft.com/en-us/visualstudio/code-quality/install-fxcop-analyzers). It also contains new analyzers, and for .NET 5, the .NET SDK will include a large number of these analyzers automatically, including brand new ones that have been written for this release. Multiple of these rules are either focused on or at least partially related to performance. Here are a few examples:

-   [Detecting accidental allocations as part of range indexing](https://github.com/dotnet/roslyn-analyzers/pull/3464). C# 8 introduced ranges, which make it easy to slice collections, e.g. `someCollection[1..3]`. Such an expression translates into either use of the collection’s indexer that takes a `Range`, e.g. `public MyCollection this[Range r] { get; }`, or if no such indexer is present, into use of a `Slice(int start, int length)`. By convention and design guidelines, such indexers and slice methods should return the same type over which they’re defined, so for example slicing a `T[]` produces another `T[]`, and slicing a `Span<T>` produces a `Span<T>`. This, however, can lead to unexpected allocations hiding because of implicit casts. For example, `T[]` can be implicitly cast to a `Span<T>`, but that also means that the result of slicing a `T[]` can be implicitly cast to a `Span<T>`, which means code like this `Span<T> span = _array[1..3];` will compile and run fine, except that it will incur an array allocation for the array slice produced by the `_array[1..3]` range indexing. A more efficient way to write this would be `Span<T> span = _array.AsSpan()[1..3]`. This analyzer will detect several such cases and offer fixers to eliminate the allocation.

```
[Benchmark(Baseline = true)]
public ReadOnlySpan<char> Slice1()
{
    ReadOnlySpan<char> span = "hello world"[1..3];
    return span;
}

[Benchmark]
public ReadOnlySpan<char> Slice2()
{
    ReadOnlySpan<char> span = "hello world".AsSpan()[1..3];
    return span;
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Slice1 | 8.3337 ns | 1.00 | 32 B |
| Slice2 | 0.4332 ns | 0.05 | – |

-   [Prefer `Memory` overloads for `Stream.Read/WriteAsync` methods](https://github.com/dotnet/roslyn-analyzers/pull/3497). .NET Core 2.1 added new overloads to `Stream.ReadAsync` and `Stream.WriteAsync` that operate on `Memory<byte>` and `ReadOnlyMemory<byte>`, respectively. This enables those methods to work with data from sources other than `byte[]`, and also enables optimizations like being able to avoid pinning if the `{ReadOnly}Memory<byte>` was created in a manner that specified it represented already pinned or otherwise immovable data. However, the introduction of the new overloads also enabled a new opportunity to choose the return type for these methods, and we chose `ValueTask<int>` and `ValueTask`, respectively, rather than `Task<int>` and `Task`. The benefit of that is enabling more synchronously completing calls to be allocation-free, and even more asynchronously completing calls to be allocation-free (though with more effort on the part of the developer of the override). As a result, it’s frequently beneficial to prefer the newer overloads than the older ones, and this analyzer will detect use of the old and offer fixes to automatically switch to using the newer ones. [dotnet/runtime#35941](https://github.com/dotnet/runtime/pull/35941) has some examples of this fixing cases found in dotnet/runtime.

```
private NetworkStream _client, _server;
private byte[] _buffer = new byte[10];

[GlobalSetup]
public void Setup()
{
    using Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    listener.Listen();
    client.Connect(listener.LocalEndPoint);
    _client = new NetworkStream(client);
    _server = new NetworkStream(listener.Accept());
}

[Benchmark(Baseline = true)]
public async Task ReadWrite1()
{
    byte[] buffer = _buffer;
    for (int i = 0; i < 1000; i++)
    {
        await _client.WriteAsync(buffer, 0, buffer.Length);
        await _server.ReadAsync(buffer, 0, buffer.Length); // may not read everything; just for demo purposes
    }
}

[Benchmark]
public async Task ReadWrite2()
{
    byte[] buffer = _buffer;
    for (int i = 0; i < 1000; i++)
    {
        await _client.WriteAsync(buffer);
        await _server.ReadAsync(buffer); // may not read everything; just for demo purposes
    }
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| ReadWrite1 | 7.604 ms | 1.00 | 72001 B |
| ReadWrite2 | 7.549 ms | 0.99 | – |

-   [Prefer typed overloads on `StringBuilder`](https://github.com/dotnet/roslyn-analyzers/pull/3443). `StringBuilder.Append` and `StringBuilder.Insert` have many overloads, for appending not just strings or objects but also various primitive types, like `Int32`. Even so, it’s common to see code like `stringBuilder.Append(intValue.ToString())`. The `StringBuilder.Append(Int32)` overload can be much more efficient, not requiring allocating a string, and should be preferred. This analyzer comes with a fixer to detect such cases and automatically switch to using the more appropriate overload.

```
[Benchmark(Baseline = true)]
public void Append1()
{
    _builder.Clear();
    for (int i = 0; i < 1000; i++)
        _builder.Append(i.ToString());
}

[Benchmark]
public void Append2()
{
    _builder.Clear();
    for (int i = 0; i < 1000; i++)
        _builder.Append(i);
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Append1 | 13.546 us | 1.00 | 31680 B |
| Append2 | 9.841 us | 0.73 | – |

-   [Prefer `StringBuilder.Append(char)` over `StringBuilder.Append(string)`](https://github.com/dotnet/runtime/issues/33786). Appending a single `char` to a `StringBuilder` is a bit more efficient than appending a `string` of length 1. Yet it’s fairly common to see code like `private const string Separator = ":"; ...; builder.Append(Separator);`, and this would be better if the const were changed to be `private const char Separator = ':';`. The analyzer will flag many such cases and help to fix them. Some examples of this being fixed in dotnet/runtime in response to the analyzer are in [dotnet/runtime#36097](https://github.com/dotnet/runtime/pull/36097).

```
[Benchmark(Baseline = true)]
public void Append1()
{
    _builder.Clear();
    for (int i = 0; i < 1000; i++)
        _builder.Append(":");
}

[Benchmark]
public void Append2()
{
    _builder.Clear();
    for (int i = 0; i < 1000; i++)
        _builder.Append(':');
}
```

| Method | Mean | Ratio |
| --- | --- | --- |
| Append1 | 2.621 us | 1.00 |
| Append2 | 1.968 us | 0.75 |

-   [Prefer `IsEmpty` over `Count`](https://github.com/dotnet/roslyn-analyzers/pull/3584). Similar to the LINQ `Any()` vs `Count()` discussion earlier, some collection types expose both an `IsEmpty` property and a `Count` property. In some cases, such as with a concurrent collection like `ConcurrentQueue<T>`, it can be much more expensive to determine an exact count of the number of items in the collection than to determine simply whether there are any items in the collection. In such cases, if code was written to do a check like `if (collection.Count != 0)`, it can be more efficient to instead be `if (!collection.IsEmpty)`. This analyzer helps to find such cases and fix them.

```
[Benchmark(Baseline = true)]
public bool IsEmpty1() => _queue.Count == 0;

[Benchmark]
public bool IsEmpty2() => _queue.IsEmpty;
```

| Method | Mean | Ratio |
| --- | --- | --- |
| IsEmpty1 | 21.621 ns | 1.00 |
| IsEmpty2 | 4.041 ns | 0.19 |

-   [Prefer `Environment.ProcessId`](https://github.com/dotnet/roslyn-analyzers/pull/3838). [dotnet/runtime#38908](https://github.com/dotnet/runtime/pull/38908) added a new static property `Environment.ProcessId`, which returns the current process’ id. It’s common to see code that previously tried to do the same thing with `Process.GetCurrentProcess().Id`. The latter, however, is significantly less efficient, allocating a finalizable object and making a system call on every invocation, and in a manner that can’t easily support internal caching. This new analyzer helps to automatically find and replace such usage.

```
[Benchmark(Baseline = true)]
public int PGCPI() => Process.GetCurrentProcess().Id;

[Benchmark]
public int EPI() => Environment.ProcessId;
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| PGCPI | 67.856 ns | 1.00 | 280 B |
| EPI | 3.191 ns | 0.05 | – |

-   [Avoid stackalloc in loops](https://github.com/dotnet/roslyn-analyzers/pull/3432). This analyzer doesn’t so much help you to make your code faster, but rather helps you to make your code correct when you’ve employed solutions for making your code faster. Specifically, it flags cases where `stackalloc` is used to allocate memory from the stack, but where it’s used in a loop. The memory allocated from the stack as part of a `stackalloc` may not be released until the method returns, so if `stackalloc` is used in a loop, it can potentially result in allocating much more memory than the developer intended, and eventually result in a stack overflow that crashes the process. You can see a few examples of this being fixed in [dotnet/runtime#34149](https://github.com/dotnet/runtime/pull/34149).

## What’s Next?

Per the [.NET roadmap](https://github.com/dotnet/core/blob/master/roadmap.md), .NET 5 is scheduled to be released in November 2020, which is still several months away. And while this post has demonstrated a huge number of performance advancements already in for the release, I expect we’ll see a plethora of additional performance improvements find there way into .NET 5, if for no other reason than there are currently PRs pending for a bunch (beyond the ones previously mentioned in other discussions), e.g. [dotnet/runtime#34864](https://github.com/dotnet/runtime/pull/34864) and [dotnet/runtime#32552](https://github.com/dotnet/runtime/pull/32552) further improve `Uri`, [dotnet/runtime#402](https://github.com/dotnet/runtime/pull/402) vectorizes `string.Compare` for ordinal comparisons, [dotnet/runtime#36252](https://github.com/dotnet/runtime/pull/36252) improves the performance of `Dictionary<TKey, TValue>` lookups with `OrdinalIgnoreCase` by extending the existing non-randomization optimization to case-insensitivity, [dotnet/runtime#34633](https://github.com/dotnet/runtime/pull/34633) provides an asynchronous implementation of DNS resolution on Linux, [dotnet/runtime#32520](https://github.com/dotnet/runtime/pull/32520) significantly reduces the overhead of `Activator.CreateInstance<T>()`, [dotnet/runtime#32843](https://github.com/dotnet/runtime/pull/32843) makes `Utf8Parser.TryParse` faster for Int32 values, [dotnet/runtime#35654](https://github.com/dotnet/runtime/pull/35654) improves the performance of `Guid` equality checks, [dotnet/runtime#39117](https://github.com/dotnet/runtime/pull/39117) reduces costs for `EventListeners` handling `EventSource` events, and [dotnet/runtime#38896](https://github.com/dotnet/runtime/pull/38896) from [@Bond-009](https://github.com/Bond-009) special-cases more inputs to `Task.WhenAny`.

Finally, while we try really hard to avoid performance regressions, any release will invariably have some, and we’ll be spending time investigating ones we find. One known class of such regressions has to do with a feature enabled in .NET 5: ICU. .NET Framework and previous releases of .NET Core on Windows have used [National Language Support (NLS)](https://docs.microsoft.com/en-us/windows/win32/intl/national-language-support) APIs for globalization on Windows, whereas .NET Core on Unix has used [International Components for Unicode (ICU)](http://site.icu-project.org/). .NET 5 [switches to use ICU by default](https://docs.microsoft.com/en-us/dotnet/standard/globalization-localization/globalization-icu) on all operating systems if it’s available (Windows 10 includes it as of the May 2019 Update), enabling much better behavior consistency across OSes. However, since these two technologies have different performance profiles, some operations (in particular culture-aware string operations) may end up being slower in some cases. While we hope to mitigate most of these (which should also help to improve performance on Linux and macOS), and while any that do remain are likely to be inconsequential for your apps, you can [opt to continue using NLS](https://docs.microsoft.com/en-us/dotnet/standard/globalization-localization/globalization-icu#using-nls-instead-of-icu) if the changes negatively impact your particular application.

With [.NET 5 previews](https://dotnet.microsoft.com/download/dotnet/5.0) and [nightly builds](https://github.com/dotnet/installer/blob/master/README.md#installers-and-binaries) available, I’d encourage you to download the latest bits and give them a whirl with your applications. And if you find things you think can and should be improved, we’d welcome your PRs to dotnet/runtime!

Happy coding!

## Author

![Stephen Toub - MSFT](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2022/03/stoub_square-96x96.jpg)

Partner Software Engineer

Stephen Toub is a developer on the .NET team at Microsoft.